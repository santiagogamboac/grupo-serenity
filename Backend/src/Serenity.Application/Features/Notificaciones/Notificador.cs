using System.Text;
using Microsoft.Extensions.Logging;
using Serenity.Application.Common;
using Serenity.Application.Features.Reservas;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Notificaciones;

/// <summary>Sección <c>Notificaciones</c>: a quién avisar y cómo firmar.</summary>
public sealed class OpcionesNotificaciones
{
    /// <summary>Correos del equipo que reciben los avisos, separados por coma.</summary>
    public string CorreoEquipo { get; init; } = "";

    public string NombreSitio { get; init; } = "Grupo Serenity";

    public IReadOnlyList<string> Equipo =>
        CorreoEquipo.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Avisos por correo (lo que hacía Contact Form 7 en el WordPress). Nunca rompe el flujo que lo llama:
/// un fallo del servidor de correo se registra y se sigue.
/// </summary>
public sealed class Notificador(
    IEnviadorCorreo correo, OpcionesNotificaciones opciones, OpcionesReservas reservas, ILogger<Notificador> log)
{
    public Task NuevaSolicitudAsync(long id, string nombre, string contacto, DateOnly llegada, int huespedes, string destino, CancellationToken ct)
    {
        var t = new StringBuilder()
            .AppendLine("Llegó una solicitud nueva desde el formulario del sitio.").AppendLine()
            .AppendLine($"Nombre: {nombre}")
            .AppendLine($"Contacto: {contacto}")
            .AppendLine($"Destino: {destino}")
            .AppendLine($"Llegada: {FormatoColombia.Fecha(llegada)}")
            .AppendLine($"Huéspedes: {huespedes}").AppendLine()
            .AppendLine($"Gestiónala en el panel: {Panel($"/admin/solicitudes")} (solicitud #{id}).");

        var responderA = contacto.Contains('@') ? contacto : null;
        return AlEquipoAsync($"Nueva solicitud de {nombre}", t.ToString(), responderA, ct);
    }

    public Task NuevaReservaAsync(ReservaRegistro r, bool pagoEnLinea, CancellationToken ct)
    {
        var t = new StringBuilder()
            .AppendLine(pagoEnLinea
                ? "Se creó una reserva en el sitio. Las fechas quedan retenidas mientras el huésped paga en Wompi."
                : "Se creó una reserva en el sitio. El pago en línea está apagado: contacta al huésped para cobrar y confírmala en el panel.")
            .AppendLine()
            .Append(Resumen(r)).AppendLine()
            .AppendLine($"Retención hasta: {(r.ExpiraEn is { } e ? FormatoColombia.FechaHora(e) : "—")}")
            .AppendLine($"Panel: {Panel("/admin/reservas")}");

        return AlEquipoAsync($"Reserva {r.Codigo} creada · {r.AlojamientoNombre}", t.ToString(), r.Correo, ct);
    }

    public async Task ReservaConfirmadaAsync(ReservaRegistro r, CancellationToken ct)
    {
        var huesped = new StringBuilder()
            .AppendLine($"Hola, {r.Nombre}:").AppendLine()
            .AppendLine($"Tu reserva en {r.ProyectoNombre} está confirmada. ¡Te esperamos!").AppendLine()
            .Append(Resumen(r)).AppendLine()
            .AppendLine($"Consulta tu reserva cuando quieras: {reservas.EnlaceReserva(r.Codigo, r.TokenConsulta)}").AppendLine()
            .AppendLine($"Si necesitas algo, responde a este correo. — {opciones.NombreSitio}");

        await EnviarAsync(new MensajeCorreo([r.Correo], $"Reserva confirmada {r.Codigo} · {opciones.NombreSitio}", huesped.ToString(),
            opciones.Equipo.FirstOrDefault()), ct);

        var equipo = new StringBuilder()
            .AppendLine(r.PagoProveedor == "wompi" ? "Wompi aprobó el pago de esta reserva." : "Se confirmó esta reserva desde el panel.")
            .AppendLine().Append(Resumen(r))
            .AppendLine($"Pago: {r.PagoMetodo ?? r.PagoProveedor ?? "—"} {r.PagoTransaccionId}");
        await AlEquipoAsync($"Reserva {r.Codigo} confirmada · {r.AlojamientoNombre}", equipo.ToString(), r.Correo, ct);
    }

    public Task ReservaEnRevisionAsync(ReservaRegistro r, string motivo, CancellationToken ct)
    {
        var t = new StringBuilder()
            .AppendLine("Una reserva necesita revisión manual:").AppendLine(motivo).AppendLine()
            .Append(Resumen(r))
            .AppendLine($"Transacción Wompi: {r.PagoTransaccionId ?? "—"}")
            .AppendLine($"Panel: {Panel("/admin/reservas")}");
        return AlEquipoAsync($"Revisar reserva {r.Codigo}", t.ToString(), r.Correo, ct);
    }

    public Task ReservaCanceladaAsync(ReservaRegistro r, string motivo, CancellationToken ct)
    {
        var t = new StringBuilder()
            .AppendLine($"Hola, {r.Nombre}:").AppendLine()
            .AppendLine($"Tu reserva {r.Codigo} en {r.ProyectoNombre} fue cancelada.")
            .AppendLine($"Motivo: {motivo}").AppendLine()
            .Append(Resumen(r)).AppendLine()
            .AppendLine($"Si tienes dudas, responde a este correo. — {opciones.NombreSitio}");
        return EnviarAsync(new MensajeCorreo([r.Correo], $"Reserva {r.Codigo} cancelada · {opciones.NombreSitio}", t.ToString(),
            opciones.Equipo.FirstOrDefault()), ct);
    }

    internal static string Resumen(ReservaRegistro r) => new StringBuilder()
        .AppendLine($"Reserva: {r.Codigo}")
        .AppendLine($"Alojamiento: {r.AlojamientoNombre} ({r.ProyectoNombre})")
        .AppendLine($"Llegada: {FormatoColombia.Fecha(r.Llegada)}")
        .AppendLine($"Salida: {FormatoColombia.Fecha(r.Salida)}")
        .AppendLine($"Noches: {r.Noches} · Huéspedes: {r.Huespedes}")
        .AppendLine($"Huésped: {r.Nombre} · {r.Correo} · {r.Telefono}")
        .AppendLine($"Total: {FormatoColombia.Pesos(r.Total)} {r.Moneda}")
        .ToString();

    private string Panel(string ruta) => reservas.UrlSitio.TrimEnd('/') + ruta;

    private Task AlEquipoAsync(string asunto, string texto, string? responderA, CancellationToken ct)
        => opciones.Equipo.Count == 0
            ? Task.CompletedTask
            : EnviarAsync(new MensajeCorreo(opciones.Equipo, asunto, texto, responderA), ct);

    private async Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        if (!correo.Habilitado)
        {
            log.LogInformation("Correo no configurado: se omite el aviso «{Asunto}».", m.Asunto);
            return;
        }

        try
        {
            await correo.EnviarAsync(m, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "No se pudo enviar el aviso «{Asunto}».", m.Asunto);
        }
    }
}
