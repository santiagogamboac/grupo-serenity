using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Serenity.Application.Features.Reservas;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Infrastructure.Services;

/// <summary>Consulta transacciones en la API de Wompi (sandbox o producción según la configuración).</summary>
internal sealed class HttpWompiClient(OpcionesWompi opciones, ILogger<HttpWompiClient> log) : IWompiClient
{
    private static readonly HttpClient Http = new(new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(8) })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "Serenity-Reservas/1.0" } }
    };

    public async Task<TransaccionPago?> ObtenerTransaccionAsync(string id, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"{opciones.UrlApi}/transactions/{Uri.EscapeDataString(id)}");
        if (!string.IsNullOrWhiteSpace(opciones.LlavePrivada))
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opciones.LlavePrivada);
        }

        try
        {
            using var respuesta = await Http.SendAsync(peticion, ct);
            if (respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                log.LogWarning("Wompi respondió HTTP {Status} al consultar la transacción {TxId}.", (int)respuesta.StatusCode, id);
                return null;
            }

            using var json = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return json.RootElement.TryGetProperty("data", out var data) ? Wompi.LeerTransaccion(data) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            log.LogWarning("No se pudo consultar la transacción {TxId} en Wompi: {Motivo}", id, ex.Message);
            return null;
        }
    }
}
