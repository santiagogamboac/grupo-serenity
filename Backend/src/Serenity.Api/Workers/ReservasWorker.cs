using Serenity.Application.Features.Reservas;
using Serenity.Application.Interfaces;

namespace Serenity.Api.Workers;

/// <summary>
/// Cada minuto libera las fechas de las reservas cuya retención venció y reconsulta en Wompi los pagos que quedaron
/// en PENDING (PSE, Nequi…), por si el webhook no llegó.
/// </summary>
public sealed class ReservasWorker(IServiceScopeFactory scopes, OpcionesWompi wompi, ILogger<ReservasWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), ct);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    await VueltaAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Falló una vuelta del mantenimiento de reservas.");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // Apagado de la aplicación.
        }
    }

    private async Task VueltaAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var reservas = scope.ServiceProvider.GetRequiredService<IReservaRepository>();

        if (wompi.Habilitado)
        {
            var cliente = scope.ServiceProvider.GetRequiredService<IWompiClient>();
            var procesador = scope.ServiceProvider.GetRequiredService<ProcesadorPagos>();
            foreach (var r in await reservas.PendientesConTransaccionAsync(ct))
            {
                if (await cliente.ObtenerTransaccionAsync(r.PagoTransaccionId!, ct) is { } tx)
                {
                    await procesador.AplicarAsync(tx, ct);
                }
            }
        }

        var expiradas = await reservas.ExpirarVencidasAsync(ct);
        if (expiradas > 0)
        {
            log.LogInformation("{Cantidad} reservas expiraron sin pago y liberaron sus fechas.", expiradas);
        }
    }
}
