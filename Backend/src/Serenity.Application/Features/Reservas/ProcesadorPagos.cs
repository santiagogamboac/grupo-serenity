using Microsoft.Extensions.Logging;
using Serenity.Application.Common;
using Serenity.Application.Features.Notificaciones;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Domain;

namespace Serenity.Application.Features.Reservas;

/// <summary>
/// Aplica a una reserva el resultado de una transacción de Wompi, venga del webhook, de la verificación al volver del
/// checkout o del worker. Idempotente: la misma transacción aplicada dos veces no cambia nada.
/// </summary>
public sealed class ProcesadorPagos(IReservaRepository reservas, Notificador notificador, ILogger<ProcesadorPagos> log)
{
    public const string Aprobada = "APPROVED";
    public const string Pendiente = "PENDING";

    public async Task<ReservaRegistro?> AplicarAsync(TransaccionPago tx, CancellationToken ct)
    {
        var codigo = Wompi.CodigoDeReferencia(tx.Referencia);
        var r = codigo is null ? null : await reservas.ObtenerPorCodigoAsync(codigo, ct);
        if (r is null)
        {
            log.LogWarning("Transacción {TxId} con referencia desconocida {Referencia}: se ignora.", tx.Id, tx.Referencia);
            return null;
        }

        switch (tx.Estado)
        {
            case Aprobada:
                await AplicarAprobadaAsync(r, tx, ct);
                break;

            case Pendiente when r.Estado == EstadosReserva.PendientePago:
                await reservas.RegistrarPagoAsync(r.Id, tx, ct);
                break;

            case "DECLINED" or "VOIDED" or "ERROR":
                await AplicarFallidaAsync(r, tx, ct);
                break;
        }

        return await reservas.ObtenerAsync(r.Id, ct);
    }

    private async Task AplicarAprobadaAsync(ReservaRegistro r, TransaccionPago tx, CancellationToken ct)
    {
        if (r.PagoTransaccionId == tx.Id && r.PagoEstado == Aprobada)
        {
            return; // ya aplicada
        }

        if (r.Estado is EstadosReserva.Confirmada or EstadosReserva.Revision)
        {
            // Un segundo pago aprobado para la misma reserva: no se toca el estado, pero el equipo debe reembolsar.
            await reservas.AgregarNotaAsync(r.Id,
                $"Wompi aprobó otro pago ({tx.Id}, {FormatoColombia.Pesos(tx.MontoCentavos / 100)}) para esta reserva: revisar y reembolsar el sobrante.", ct);
            await notificador.ReservaEnRevisionAsync(r, $"Se recibió un segundo pago aprobado ({tx.Id}).", ct);
            return;
        }

        await reservas.RegistrarPagoAsync(r.Id, tx, ct);

        if (r.Estado == EstadosReserva.Cancelada)
        {
            await RevisionAsync(r.Id, "Se aprobó un pago para una reserva que ya estaba cancelada: reembolsar o reactivarla.", ct);
            return;
        }

        if (tx.MontoCentavos != r.Total * 100 || !string.Equals(tx.Moneda, r.Moneda, StringComparison.OrdinalIgnoreCase))
        {
            await RevisionAsync(r.Id,
                $"El monto pagado ({FormatoColombia.Pesos(tx.MontoCentavos / 100)} {tx.Moneda}) no coincide con el total de la reserva.", ct);
            return;
        }

        switch (await reservas.ConfirmarSiDisponibleAsync(r.Id, "wompi", tx, ct))
        {
            case ResultadoConfirmacion.Confirmada:
                log.LogInformation("Reserva {Codigo} confirmada con la transacción {TxId}.", r.Codigo, tx.Id);
                await notificador.ReservaConfirmadaAsync((await reservas.ObtenerAsync(r.Id, ct))!, ct);
                break;

            case ResultadoConfirmacion.FechasOcupadas:
                await RevisionAsync(r.Id, "El pago se aprobó, pero las fechas ya no estaban disponibles: reubicar al huésped o reembolsar.", ct);
                break;
        }
    }

    private async Task AplicarFallidaAsync(ReservaRegistro r, TransaccionPago tx, CancellationToken ct)
    {
        if (r.Estado == EstadosReserva.PendientePago)
        {
            // Las fechas siguen retenidas hasta que expire: el huésped puede reintentar con otro medio de pago.
            if (r.PagoEstado != Aprobada)
            {
                await reservas.RegistrarPagoAsync(r.Id, tx, ct);
            }

            return;
        }

        if (r.Estado == EstadosReserva.Confirmada && r.PagoTransaccionId == tx.Id && tx.Estado == "VOIDED")
        {
            await reservas.RegistrarPagoAsync(r.Id, tx, ct);
            await RevisionAsync(r.Id, "Wompi anuló el pago de esta reserva confirmada.", ct);
        }
    }

    private async Task RevisionAsync(long id, string motivo, CancellationToken ct)
    {
        await reservas.CambiarEstadoAsync(id, EstadosReserva.Revision, motivo, ct);
        log.LogWarning("Reserva {Id} en revisión: {Motivo}", id, motivo);
        await notificador.ReservaEnRevisionAsync((await reservas.ObtenerAsync(id, ct))!, motivo, ct);
    }
}
