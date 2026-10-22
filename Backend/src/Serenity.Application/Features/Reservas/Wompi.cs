using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Reservas;

/// <summary>Llaves de Wompi (sección <c>Wompi</c>). Sin ellas el pago en línea queda apagado.</summary>
public sealed class OpcionesWompi
{
    public string? LlavePublica { get; init; }

    /// <summary>Solo para consultar transacciones desde el servidor (al volver del checkout y en el worker).</summary>
    public string? LlavePrivada { get; init; }

    public string? SecretoIntegridad { get; init; }
    public string? SecretoEventos { get; init; }

    /// <summary><c>sandbox</c> o <c>produccion</c>.</summary>
    public string Ambiente { get; init; } = "sandbox";

    public bool Habilitado =>
        !string.IsNullOrWhiteSpace(LlavePublica)
        && !string.IsNullOrWhiteSpace(SecretoIntegridad)
        && !string.IsNullOrWhiteSpace(SecretoEventos);

    public string UrlApi => Ambiente.Equals("produccion", StringComparison.OrdinalIgnoreCase)
        ? "https://production.wompi.co/v1"
        : "https://sandbox.wompi.co/v1";
}

/// <summary>Ajustes de la reserva directa (sección <c>Reservas</c>).</summary>
public sealed class OpcionesReservas
{
    /// <summary>Minutos que se retienen las fechas mientras el huésped paga en Wompi.</summary>
    public int MinutosRetencion { get; init; } = 30;

    /// <summary>Retención cuando no hay pago en línea: el equipo confirma a mano al recibir el pago.</summary>
    public int MinutosRetencionSinPago { get; init; } = 1440;

    /// <summary>Origen público del sitio (https://...), para la URL de regreso de Wompi y los enlaces de los correos.</summary>
    public string UrlSitio { get; init; } = "http://localhost:8090";

    public string EnlaceReserva(string codigo, string token)
        => $"{UrlSitio.TrimEnd('/')}/reserva/{Uri.EscapeDataString(codigo)}?t={Uri.EscapeDataString(token)}";
}

public static class Wompi
{
    public const string UrlCheckout = "https://checkout.wompi.co/p/";

    /// <summary>Formato ISO 8601 UTC con milisegundos, igual al de la documentación (2023-06-09T20:28:50.000Z).</summary>
    public static string FormatoExpiracion(DateTime utc)
        => utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>SHA256 hex de <c>referencia + centavos + moneda [+ expiración] + secreto</c>.</summary>
    public static string FirmaIntegridad(string referencia, long centavos, string moneda, string? expiracion, string secreto)
        => Sha256Hex(referencia + centavos.ToString(CultureInfo.InvariantCulture) + moneda + (expiracion ?? "") + secreto);

    /// <summary>URL del Web Checkout (redirección) con la firma de integridad calculada en el servidor.</summary>
    public static string ConstruirUrlCheckout(OpcionesWompi o, ReservaRegistro r, string referencia, string urlRegreso)
    {
        var centavos = r.Total * 100;
        var expiracion = r.ExpiraEn is { } e ? FormatoExpiracion(e) : null;
        var firma = FirmaIntegridad(referencia, centavos, r.Moneda, expiracion, o.SecretoIntegridad!);

        var parametros = new List<(string, string)>
        {
            ("public-key", o.LlavePublica!),
            ("currency", r.Moneda),
            ("amount-in-cents", centavos.ToString(CultureInfo.InvariantCulture)),
            ("reference", referencia),
            ("signature:integrity", firma),
            ("redirect-url", urlRegreso),
            ("customer-data:email", r.Correo),
            ("customer-data:full-name", r.Nombre)
        };
        if (expiracion is not null)
        {
            parametros.Add(("expiration-time", expiracion));
        }

        return UrlCheckout + "?" + string.Join("&", parametros.Select(p => $"{p.Item1}={Uri.EscapeDataString(p.Item2)}"));
    }

    /// <summary>Referencia de un intento de pago: el código de la reserva y el número de intento.</summary>
    public static string Referencia(string codigo, int intento) => $"{codigo}-{intento}";

    /// <summary>Código de reserva a partir de una referencia <c>SER-XXXXXX-N</c>; null si no tiene esa forma.</summary>
    public static string? CodigoDeReferencia(string? referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia)) return null;
        var i = referencia.LastIndexOf('-');
        return i > 0 && int.TryParse(referencia.AsSpan(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _)
            ? referencia[..i]
            : null;
    }

    /// <summary>
    /// Valida el checksum de un evento: SHA256 de los valores de <c>signature.properties</c> (rutas dentro de
    /// <c>data</c>), luego <c>timestamp</c> y el secreto de eventos. Devuelve la transacción si es válido.
    /// </summary>
    public static TransaccionPago? LeerEventoValido(JsonElement evento, string secretoEventos)
    {
        if (evento.ValueKind != JsonValueKind.Object
            || !evento.TryGetProperty("data", out var data)
            || !evento.TryGetProperty("signature", out var firma)
            || !firma.TryGetProperty("properties", out var propiedades) || propiedades.ValueKind != JsonValueKind.Array
            || !firma.TryGetProperty("checksum", out var checksum) || checksum.ValueKind != JsonValueKind.String
            || !evento.TryGetProperty("timestamp", out var timestamp))
        {
            return null;
        }

        var texto = new StringBuilder();
        foreach (var propiedad in propiedades.EnumerateArray())
        {
            if (propiedad.ValueKind != JsonValueKind.String || Navegar(data, propiedad.GetString()!) is not { } valor)
            {
                return null;
            }

            texto.Append(ValorPlano(valor));
        }

        texto.Append(ValorPlano(timestamp)).Append(secretoEventos);

        var esperado = Encoding.ASCII.GetBytes(Sha256Hex(texto.ToString()));
        var recibido = Encoding.ASCII.GetBytes(checksum.GetString()!.ToLowerInvariant());
        if (!CryptographicOperations.FixedTimeEquals(esperado, recibido))
        {
            return null;
        }

        return data.TryGetProperty("transaction", out var tx) ? LeerTransaccion(tx) : null;
    }

    /// <summary>Lee el objeto <c>transaction</c> (del evento o de <c>GET /transactions/{id}</c>).</summary>
    public static TransaccionPago? LeerTransaccion(JsonElement tx)
    {
        string? Texto(string nombre) => tx.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var id = Texto("id");
        var referencia = Texto("reference");
        var estado = Texto("status");
        if (id is null || referencia is null || estado is null
            || !tx.TryGetProperty("amount_in_cents", out var monto) || !monto.TryGetInt64(out var centavos))
        {
            return null;
        }

        return new TransaccionPago(id, referencia, estado, centavos, Texto("currency") ?? "COP", Texto("payment_method_type"));
    }

    private static JsonElement? Navegar(JsonElement raiz, string ruta)
    {
        var actual = raiz;
        foreach (var parte in ruta.Split('.'))
        {
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(parte, out actual))
            {
                return null;
            }
        }

        return actual;
    }

    /// <summary>Texto tal cual: strings sin comillas, números con su representación original.</summary>
    private static string ValorPlano(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString()!,
        JsonValueKind.Null => "",
        _ => v.GetRawText()
    };

    private static string Sha256Hex(string s)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
