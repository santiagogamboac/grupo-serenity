using System.Net;
using System.Text;
using Serenity.Application.Interfaces;

namespace Serenity.Infrastructure.Services;

/// <summary>Descarga .ics con tiempo, tamaño y redirecciones acotados.</summary>
internal sealed class HttpIcalClient : IIcalClient
{
    private const int MaxBytes = 2 * 1024 * 1024;

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        ConnectTimeout = TimeSpan.FromSeconds(8)
    })
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { { "User-Agent", "Serenity-CalendarSync/1.0" }, { "Accept", "text/calendar, */*;q=0.5" } }
    };

    public async Task<string> DescargarAsync(string url, CancellationToken ct)
    {
        try
        {
            using var respuesta = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (respuesta.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"El servidor del calendario respondió HTTP {(int)respuesta.StatusCode}.");
            }

            await using var stream = await respuesta.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var bloque = new byte[16 * 1024];
            int leidos;
            while ((leidos = await stream.ReadAsync(bloque, ct)) > 0)
            {
                if (buffer.Length + leidos > MaxBytes)
                {
                    throw new InvalidOperationException("El calendario supera el tamaño máximo permitido (2 MB).");
                }

                buffer.Write(bloque, 0, leidos);
            }

            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("El calendario tardó demasiado en responder.");
        }
        catch (HttpRequestException ex)
        {
            // Sin ex.Message completo: puede traer la URL (con el secreto del iCal).
            throw new InvalidOperationException($"No se pudo descargar el calendario ({ex.HttpRequestError}).");
        }
    }
}
