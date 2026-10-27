using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Serenity.Application.Common;
using Serenity.Application.Features.Anuncios;
using Serenity.Application.Features.Reservas;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Tests;

/// <summary>Doble de prueba: cada método se resuelve por nombre; si no se configuró, falla para no ocultar llamadas inesperadas.</summary>
public class Falso<T> : DispatchProxy where T : class
{
    private readonly Dictionary<string, Func<object?[], object?>> metodos = new();

    public static (T Objeto, Falso<T> Config) Crear()
    {
        var obj = Create<T, Falso<T>>();
        return (obj, (Falso<T>)(object)obj);
    }

    public Falso<T> Con(string nombre, Func<object?[], object?> impl)
    {
        metodos[nombre] = impl;
        return this;
    }

    protected override object? Invoke(MethodInfo? metodo, object?[]? args)
        => metodos.TryGetValue(metodo!.Name, out var impl)
            ? impl(args ?? [])
            : throw new InvalidOperationException($"Llamada no configurada en el falso: {typeof(T).Name}.{metodo.Name}");
}

public class FeedIcalTests
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTime Ahora = new(2027, 3, 1, 15, 0, 0, DateTimeKind.Utc);

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private static IcalAlojamientoQueryHandler Handler(params OcupacionAlojamiento[] ocupado)
    {
        var alojamiento = new AlojamientoRegistro { Id = 4, Nombre = "Cabaña", TokenIcal = Token, Publicado = true };
        var (alojamientos, cfg) = Falso<IAlojamientoRepository>.Crear();
        cfg.Con(nameof(IAlojamientoRepository.ObtenerPorTokenIcalAsync),
               a => Task.FromResult((string)a[0]! == Token ? alojamiento : null))
           .Con(nameof(IAlojamientoRepository.OcupacionAsync),
               _ => Task.FromResult<IReadOnlyList<OcupacionAlojamiento>>(ocupado));
        var (reservas, cfgR) = Falso<IReservaRepository>.Crear();
        cfgR.Con(nameof(IReservaRepository.ExpirarVencidasAsync), _ => Task.FromResult(0));
        return new IcalAlojamientoQueryHandler(alojamientos, reservas, new RelojFijo(Ahora));
    }

    private static OcupacionAlojamiento Ocupado(string origen, string referencia, int diaDesde, int diaHasta) => new()
    {
        AlojamientoId = 4, Origen = origen, Referencia = referencia,
        Desde = new DateOnly(2027, 3, diaDesde), Hasta = new DateOnly(2027, 3, diaHasta)
    };

    [Fact]
    public async Task Token_valido_devuelve_un_ics_con_reservas_y_bloqueos_manuales_pero_sin_los_de_airbnb()
    {
        var r = await Handler(
            Ocupado("reserva", "SER-AAA222", 5, 8),
            Ocupado("manual", "3", 10, 11),
            Ocupado("airbnb", "x@airbnb.com", 20, 25)).Handle(new IcalAlojamientoQuery(Token), CancellationToken.None);

        Assert.True(r.IsSuccess);
        var ics = r.Value!;
        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
        Assert.DoesNotContain("airbnb", ics, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, IcalParser.LeerBloqueos(ics).Count);
        Assert.Contains("DTSTART;VALUE=DATE:20270305\r\n", ics);
        Assert.Contains("DTEND;VALUE=DATE:20270308\r\n", ics); // exclusivo: día de salida
    }

    [Theory]
    [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffff")] // largo válido, otro token
    [InlineData("corto")]                                             // ni siquiera se consulta
    public async Task Token_invalido_es_404(string token)
    {
        var r = await Handler().Handle(new IcalAlojamientoQuery(token), CancellationToken.None);

        Assert.True(r.IsFailure);
        Assert.Equal(ErrorTipo.NoEncontrado, r.Tipo);
    }

    [Fact]
    public void Todas_las_lineas_terminan_en_CRLF_y_no_hay_datos_del_huesped()
    {
        var a = new AlojamientoRegistro { Id = 1, Nombre = "Casa", TokenIcal = "t" };
        var ics = IcalWriter.Escribir(a, [Ocupado("reserva", "SER-ZZZ999", 1, 3)], Ahora);

        Assert.DoesNotContain("\n", ics.Replace("\r\n", ""));
        Assert.Contains("DTSTAMP:20270301T150000Z\r\n", ics);
        Assert.DoesNotContain("@", ics.Replace("@gruposerenity", "")); // ni correos ni nada parecido
    }
}

public class IcalAirbnbTests
{
    /// <summary>Forma real del export de Airbnb: reservas ("Reserved") y noches cerradas por el anfitrión ("Not available").</summary>
    private const string ExportAirbnb =
        "BEGIN:VCALENDAR\r\n" +
        "PRODID;X-RICAL-TZSOURCE=TZINFO:-//Airbnb Inc//Hosting Calendar 1.0//EN\r\n" +
        "CALSCALE:GREGORIAN\r\n" +
        "VERSION:2.0\r\n" +
        "BEGIN:VEVENT\r\n" +
        "DTEND;VALUE=DATE:20270315\r\n" +
        "DTSTART;VALUE=DATE:20270312\r\n" +
        "UID:1418fb94e984-0d6e1f6ea4b1b1e5c6ba7c0b7d1f0a9b@airbnb.com\r\n" +
        "DESCRIPTION:Reservation URL: https://www.airbnb.com/hosting/reservations/details/HMABCDEF12\\nPhone Numbe\r\n" +
        " r (Last 4 Digits): 1234\r\n" +
        "SUMMARY:Reserved\r\n" +
        "END:VEVENT\r\n" +
        "BEGIN:VEVENT\r\n" +
        "DTEND;VALUE=DATE:20270401\r\n" +
        "DTSTART;VALUE=DATE:20270320\r\n" +
        "UID:7f662ec65913-a7c6c1b4ad2cde8a0b2a1f0f5b4c3d2e@airbnb.com\r\n" +
        "SUMMARY:Airbnb (Not available)\r\n" +
        "END:VEVENT\r\n" +
        "END:VCALENDAR\r\n";

    [Fact]
    public void Lee_reservas_y_noches_no_disponibles_con_su_uid_y_fin_exclusivo()
    {
        var b = IcalParser.LeerBloqueos(ExportAirbnb);

        Assert.Equal(2, b.Count);
        Assert.Equal(new BloqueoIcal("1418fb94e984-0d6e1f6ea4b1b1e5c6ba7c0b7d1f0a9b@airbnb.com", new(2027, 3, 12), new(2027, 3, 15)), b[0]);
        Assert.Equal(new DateOnly(2027, 3, 20), b[1].Desde);
        Assert.Equal(new DateOnly(2027, 4, 1), b[1].Hasta);
    }
}

public class ChoqueConBloqueosTests
{
    private static CalendarioAlojamientos Calendario(params OcupacionAlojamiento[] ocupado)
    {
        var (alojamientos, cfg) = Falso<IAlojamientoRepository>.Crear();
        cfg.Con(nameof(IAlojamientoRepository.OcupacionAsync), _ => Task.FromResult<IReadOnlyList<OcupacionAlojamiento>>(ocupado));
        var (anuncios, _) = Falso<IAnuncioRepository>.Crear();
        var sync = new AnuncioSyncService(anuncios, Falso<IIcalClient>.Crear().Objeto, NullLogger<AnuncioSyncService>.Instance);
        return new CalendarioAlojamientos(alojamientos, anuncios, sync, new OpcionesAirbnb());
    }

    private static OcupacionAlojamiento Bloqueo(string origen, int desde, int hasta)
        => new() { AlojamientoId = 1, Origen = origen, Referencia = "r", Desde = new(2027, 5, desde), Hasta = new(2027, 5, hasta) };

    [Theory]
    [InlineData(10, 12, true)]  // misma noche
    [InlineData(8, 11, true)]   // se monta por delante
    [InlineData(5, 10, false)]  // sale el día que empieza el bloqueo
    [InlineData(12, 14, false)] // llega el día en que el bloqueo termina
    public async Task Regla_de_cruce_semiabierta_contra_bloqueos_de_airbnb(int llegada, int salida, bool choca)
    {
        var c = Calendario(Bloqueo("airbnb", 10, 12));
        Assert.Equal(choca, await c.ChocaConBloqueosAsync(1, new(2027, 5, llegada), new(2027, 5, salida), CancellationToken.None));
    }

    [Fact]
    public async Task La_propia_reserva_no_cuenta_como_choque_pero_un_bloqueo_manual_si()
    {
        Assert.False(await Calendario(Bloqueo("reserva", 10, 12)).ChocaConBloqueosAsync(1, new(2027, 5, 10), new(2027, 5, 12), CancellationToken.None));
        Assert.True(await Calendario(Bloqueo("manual", 11, 13)).ChocaConBloqueosAsync(1, new(2027, 5, 10), new(2027, 5, 12), CancellationToken.None));
    }
}
