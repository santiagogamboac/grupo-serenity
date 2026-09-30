using System.Globalization;
using System.Text.RegularExpressions;

namespace Serenity.Application.Common;

/// <summary>Noches bloqueadas [Desde, Hasta): <c>Hasta</c> es el día de salida (DTEND de iCal, exclusivo).</summary>
public sealed record BloqueoIcal(string? Uid, DateOnly Desde, DateOnly Hasta);

/// <summary>Lector mínimo de iCal (RFC 5545): solo lo que hace falta para leer los VEVENT de Airbnb.</summary>
public static class IcalParser
{
    public static bool EsCalendario(string contenido)
        => contenido.TrimStart('﻿', ' ', '\r', '\n', '\t').StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lee los eventos del calendario. Lanza <see cref="FormatException"/> si no es un iCal.</summary>
    public static IReadOnlyList<BloqueoIcal> LeerBloqueos(string contenido)
    {
        if (!EsCalendario(contenido))
        {
            throw new FormatException("La respuesta no es un calendario iCal (falta BEGIN:VCALENDAR).");
        }

        var resultado = new List<BloqueoIcal>();
        var enEvento = false;
        string? uid = null, dtStart = null, dtEnd = null, duracion = null;

        foreach (var linea in Desplegar(contenido))
        {
            if (linea.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                enEvento = true;
                uid = dtStart = dtEnd = duracion = null;
                continue;
            }

            if (linea.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                enEvento = false;
                var bloqueo = Construir(uid, dtStart, dtEnd, duracion);
                if (bloqueo is not null)
                {
                    resultado.Add(bloqueo);
                }

                continue;
            }

            if (!enEvento)
            {
                continue;
            }

            var dosPuntos = linea.IndexOf(':');
            if (dosPuntos <= 0)
            {
                continue;
            }

            // El nombre puede traer parámetros: DTSTART;VALUE=DATE:20260101
            var nombre = linea[..dosPuntos];
            var parametros = nombre.IndexOf(';');
            var propiedad = (parametros >= 0 ? nombre[..parametros] : nombre).ToUpperInvariant();
            var valor = linea[(dosPuntos + 1)..].Trim();

            switch (propiedad)
            {
                case "UID": uid = valor; break;
                case "DTSTART": dtStart = valor; break;
                case "DTEND": dtEnd = valor; break;
                case "DURATION": duracion = valor; break;
            }
        }

        return resultado;
    }

    private static BloqueoIcal? Construir(string? uid, string? dtStart, string? dtEnd, string? duracion)
    {
        if (!TryFecha(dtStart, out var desde))
        {
            return null;
        }

        DateOnly hasta;
        if (TryFecha(dtEnd, out var fin))
        {
            hasta = fin;
        }
        else if (duracion is not null && TryDias(duracion, out var dias))
        {
            hasta = desde.AddDays(dias);
        }
        else
        {
            hasta = desde.AddDays(1); // RFC: sin DTEND ni DURATION, un evento de fecha dura un día.
        }

        // Evento de horas dentro del mismo día (DTEND == DTSTART): cuenta como esa noche.
        if (hasta <= desde)
        {
            hasta = desde.AddDays(1);
        }

        return new BloqueoIcal(uid, desde, hasta);
    }

    /// <summary>Acepta YYYYMMDD y YYYYMMDDTHHMMSS[Z]; se toma la fecha tal cual, sin convertir zonas.</summary>
    private static bool TryFecha(string? valor, out DateOnly fecha)
    {
        fecha = default;
        if (valor is null || valor.Length < 8)
        {
            return false;
        }

        return DateOnly.TryParseExact(valor[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }

    private static readonly Regex DuracionRegex = new(@"^P(?:(\d+)W)?(?:(\d+)D)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool TryDias(string valor, out int dias)
    {
        dias = 0;
        var m = DuracionRegex.Match(valor);
        if (!m.Success || (!m.Groups[1].Success && !m.Groups[2].Success))
        {
            return false;
        }

        if (m.Groups[1].Success) dias += int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) * 7;
        if (m.Groups[2].Success) dias += int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        return dias > 0;
    }

    /// <summary>Une las líneas plegadas (las que continúan con espacio o tab).</summary>
    private static IEnumerable<string> Desplegar(string contenido)
    {
        string? actual = null;
        foreach (var cruda in contenido.Split('\n'))
        {
            var linea = cruda.TrimEnd('\r');
            if (linea.Length > 0 && (linea[0] == ' ' || linea[0] == '\t') && actual is not null)
            {
                actual += linea[1..];
                continue;
            }

            if (actual is not null)
            {
                yield return actual;
            }

            actual = linea;
        }

        if (actual is not null)
        {
            yield return actual;
        }
    }
}

/// <summary>Reglas sobre las URLs de Airbnb (anuncio e iCal).</summary>
public static partial class AirbnbUrls
{
    // Solo dominios de Airbnb: el servidor descargará esta URL, así que no se admite un host cualquiera (SSRF).
    [GeneratedRegex(@"^(www\.)?airbnb\.[a-z]{2,3}(\.[a-z]{2})?$", RegexOptions.IgnoreCase)]
    private static partial Regex HostAirbnb();

    [GeneratedRegex(@"^/rooms/(\d{1,25})/?$", RegexOptions.IgnoreCase)]
    private static partial Regex RutaAnuncio();

    [GeneratedRegex(@"^/calendar/ical/(\d{1,25})\.ics$", RegexOptions.IgnoreCase)]
    private static partial Regex RutaIcal();

    /// <summary>Id del anuncio a partir de https://www.airbnb.com.co/rooms/12345?... o null si no es un anuncio de Airbnb.</summary>
    public static string? IdDeAnuncio(string? url)
    {
        if (!TryHttps(url, out var uri) || !HostAirbnb().IsMatch(uri.Host))
        {
            return null;
        }

        var m = RutaAnuncio().Match(uri.AbsolutePath);
        return m.Success ? m.Groups[1].Value : null;
    }

    public static bool EsHostAirbnbHttps(string? url) => TryHttps(url, out var uri) && HostAirbnb().IsMatch(uri.Host);

    /// <summary>Id embebido en la ruta /calendar/ical/{id}.ics; null si la ruta tiene otra forma.</summary>
    public static string? IdDeIcal(string? url)
        => TryHttps(url, out var uri) && RutaIcal().Match(uri.AbsolutePath) is { Success: true } m ? m.Groups[1].Value : null;

    private static bool TryHttps(string? url, out Uri uri)
    {
        uri = null!;
        return Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && (uri = u) is not null;
    }
}
