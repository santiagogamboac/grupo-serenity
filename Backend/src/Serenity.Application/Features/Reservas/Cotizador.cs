using Serenity.Application.Common;
using Serenity.Application.Models;

namespace Serenity.Application.Features.Reservas;

/// <summary>Noches consecutivas con el mismo precio (y la temporada que lo fija, si aplica).</summary>
public sealed record TramoPrecioDto(DateOnly Desde, DateOnly Hasta, int Noches, long PrecioNoche, string? Temporada);

public sealed record CotizacionDto(
    long AlojamientoId, DateOnly Llegada, DateOnly Salida, int Huespedes, int Noches,
    IReadOnlyList<TramoPrecioDto> Tramos, int HuespedesExtra, long RecargoHuespedes,
    long Subtotal, long TarifaAseo, long Total, string Moneda);

/// <summary>Precio de una estancia. Puro: sin base de datos ni reloj (recibe "hoy").</summary>
public static class Cotizador
{
    public const int MaxNoches = 60;
    public const string Moneda = "COP";

    public static Result<CotizacionDto> Cotizar(
        AlojamientoRegistro a, IReadOnlyList<TarifaRegistro> tarifas,
        DateOnly llegada, DateOnly salida, int huespedes, DateOnly hoy)
    {
        if (llegada < hoy)
        {
            return Result.Failure<CotizacionDto>("La fecha de llegada no puede estar en el pasado.");
        }

        if (llegada > hoy.AddYears(2))
        {
            return Result.Failure<CotizacionDto>("La fecha de llegada es demasiado lejana.");
        }

        if (salida <= llegada)
        {
            return Result.Failure<CotizacionDto>("La salida debe ser posterior a la llegada.");
        }

        var noches = salida.DayNumber - llegada.DayNumber;
        if (noches > MaxNoches)
        {
            return Result.Failure<CotizacionDto>($"Se puede reservar en línea hasta {MaxNoches} noches. Para estancias más largas, escríbenos.");
        }

        if (noches < a.EstanciaMinima)
        {
            return Result.Failure<CotizacionDto>($"La estancia mínima en {a.Nombre} es de {a.EstanciaMinima} noches.");
        }

        if (huespedes < 1 || huespedes > a.Capacidad)
        {
            return Result.Failure<CotizacionDto>($"{a.Nombre} admite de 1 a {a.Capacidad} huéspedes.");
        }

        var tramos = new List<TramoPrecioDto>();
        for (var dia = llegada; dia < salida; dia = dia.AddDays(1))
        {
            var tarifa = tarifas.FirstOrDefault(t => t.Desde <= dia && dia < t.Hasta);
            var precio = tarifa?.PrecioNoche ?? a.PrecioNoche;
            var temporada = tarifa?.Nombre;

            if (tramos.Count > 0 && tramos[^1].PrecioNoche == precio && tramos[^1].Temporada == temporada)
            {
                var t = tramos[^1];
                tramos[^1] = t with { Hasta = dia.AddDays(1), Noches = t.Noches + 1 };
            }
            else
            {
                tramos.Add(new TramoPrecioDto(dia, dia.AddDays(1), 1, precio, temporada));
            }
        }

        var extra = Math.Max(0, huespedes - a.HuespedesIncluidos);
        var recargo = extra * a.RecargoHuespedExtra * noches;
        var subtotal = tramos.Sum(t => t.PrecioNoche * t.Noches) + recargo;

        return Result.Success(new CotizacionDto(
            a.Id, llegada, salida, huespedes, noches, tramos, extra, recargo,
            subtotal, a.TarifaAseo, subtotal + a.TarifaAseo, Moneda));
    }

    /// <summary>¿Algún tramo ocupado [Desde, Hasta) toca alguna noche de la estancia?</summary>
    public static bool SeCruza(IEnumerable<OcupacionRegistro> ocupado, DateOnly llegada, DateOnly salida)
        => ocupado.Any(o => o.Desde < salida && llegada < o.Hasta);
}
