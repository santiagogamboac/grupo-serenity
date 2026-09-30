using Serenity.Application.Features.Anuncios;
using Serenity.Application.Interfaces;

namespace Serenity.Api.Workers;

/// <summary>Recorre periódicamente el iCal de cada anuncio publicado y actualiza sus bloqueos.</summary>
public sealed class AirbnbSyncWorker(IServiceScopeFactory scopes, OpcionesAirbnb opciones, ILogger<AirbnbSyncWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var periodo = TimeSpan.FromMinutes(Math.Max(1, opciones.SyncMinutos));
        log.LogInformation("Sincronización de calendarios de Airbnb cada {Minutos} min.", periodo.TotalMinutes);

        try
        {
            // Pequeña espera inicial para no competir con el arranque.
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            using var timer = new PeriodicTimer(periodo);
            do
            {
                try
                {
                    await SincronizarTodosAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Falló una vuelta de sincronización de calendarios.");
                }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // Apagado de la aplicación.
        }
    }

    private async Task SincronizarTodosAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var anuncios = scope.ServiceProvider.GetRequiredService<IAnuncioRepository>();
        var sync = scope.ServiceProvider.GetRequiredService<AnuncioSyncService>();

        foreach (var anuncio in await anuncios.ListarSincronizablesAsync(ct))
        {
            // Si una visita acaba de refrescar este anuncio (o lo hace otra réplica), no se repite.
            if (await anuncios.TomarTurnoSincronizacionAsync(anuncio.Id, 30, ct))
            {
                await sync.SincronizarAsync(anuncio, ct);
            }
        }
    }
}
