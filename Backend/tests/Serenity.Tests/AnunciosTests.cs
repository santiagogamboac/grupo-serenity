using Serenity.Application.Common;
using Serenity.Application.Features.Anuncios;

namespace Serenity.Tests;

public class IcalParserTests
{
    private static string Ics(params string[] eventos)
        => "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Airbnb Inc//Hosting Calendar 1.0//EN\r\n"
           + string.Concat(eventos) + "END:VCALENDAR\r\n";

    private static string Evento(string uid, string inicio, string fin)
        => $"BEGIN:VEVENT\r\nDTEND;VALUE=DATE:{fin}\r\nDTSTART;VALUE=DATE:{inicio}\r\nUID:{uid}\r\nSUMMARY:Reserved\r\nEND:VEVENT\r\n";

    [Fact]
    public void Lee_eventos_de_dia_completo_con_fin_exclusivo()
    {
        var bloqueos = IcalParser.LeerBloqueos(Ics(Evento("a@airbnb.com", "20260110", "20260115")));

        var b = Assert.Single(bloqueos);
        Assert.Equal("a@airbnb.com", b.Uid);
        Assert.Equal(new DateOnly(2026, 1, 10), b.Desde);
        Assert.Equal(new DateOnly(2026, 1, 15), b.Hasta); // la noche del 14 es la última bloqueada
    }

    [Fact]
    public void Calendario_vacio_es_valido_y_no_tiene_bloqueos()
        => Assert.Empty(IcalParser.LeerBloqueos(Ics()));

    [Fact]
    public void Rechaza_contenido_que_no_es_ical()
        => Assert.Throws<FormatException>(() => IcalParser.LeerBloqueos("<html>Inicia sesión</html>"));

    [Fact]
    public void Sin_DTEND_dura_un_dia_y_con_DURATION_usa_esos_dias()
    {
        var sinFin = "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260301\r\nUID:x\r\nEND:VEVENT\r\n";
        var conDuracion = "BEGIN:VEVENT\r\nDTSTART;VALUE=DATE:20260305\r\nDURATION:P3D\r\nUID:y\r\nEND:VEVENT\r\n";

        var r = IcalParser.LeerBloqueos(Ics(sinFin, conDuracion));

        Assert.Equal(new DateOnly(2026, 3, 2), r[0].Hasta);
        Assert.Equal(new DateOnly(2026, 3, 8), r[1].Hasta);
    }

    [Fact]
    public void Eventos_con_hora_toman_la_fecha_sin_convertir_zonas()
    {
        var e = "BEGIN:VEVENT\r\nDTSTART:20260401T230000Z\r\nDTEND:20260403T100000Z\r\nUID:z\r\nEND:VEVENT\r\n";

        var b = Assert.Single(IcalParser.LeerBloqueos(Ics(e)));

        Assert.Equal(new DateOnly(2026, 4, 1), b.Desde);
        Assert.Equal(new DateOnly(2026, 4, 3), b.Hasta);
    }

    [Fact]
    public void Une_lineas_plegadas_y_tolera_saltos_LF_y_BOM()
    {
        var ics = "﻿BEGIN:VCALENDAR\nBEGIN:VEVENT\nUID:muy-\n largo\nDTSTART;VALUE=DATE:20260501\nDTEND;VALUE=DATE:20260502\nEND:VEVENT\nEND:VCALENDAR\n";

        var b = Assert.Single(IcalParser.LeerBloqueos(ics));

        Assert.Equal("muy-largo", b.Uid);
    }

    [Fact]
    public void Ignora_eventos_sin_fecha_valida()
    {
        var e = "BEGIN:VEVENT\r\nUID:roto\r\nDTSTART:no-es-fecha\r\nEND:VEVENT\r\n";

        Assert.Empty(IcalParser.LeerBloqueos(Ics(e)));
    }
}

public class AirbnbUrlsTests
{
    [Theory]
    [InlineData("https://www.airbnb.com.co/rooms/557255594023352394?source_impression_id=p3_1", "557255594023352394")]
    [InlineData("https://airbnb.com/rooms/123", "123")]
    [InlineData("https://www.airbnb.es/rooms/45/", "45")]
    public void Extrae_el_id_del_anuncio(string url, string esperado)
        => Assert.Equal(esperado, AirbnbUrls.IdDeAnuncio(url));

    [Theory]
    [InlineData("http://www.airbnb.com/rooms/123")]              // sin https
    [InlineData("https://evil.com/rooms/123")]                   // otro dominio
    [InlineData("https://airbnb.com.evil.com/rooms/123")]        // dominio que solo empieza como Airbnb
    [InlineData("https://www.airbnb.com/users/show/123")]        // no es un anuncio
    [InlineData("")]
    [InlineData(null)]
    public void Rechaza_lo_que_no_es_un_anuncio_de_airbnb(string? url)
        => Assert.Null(AirbnbUrls.IdDeAnuncio(url));

    [Fact]
    public void Extrae_el_id_de_la_ruta_del_ical()
        => Assert.Equal("777", AirbnbUrls.IdDeIcal("https://www.airbnb.com/calendar/ical/777.ics?s=abc"));

    [Fact]
    public void Ical_con_otra_forma_de_ruta_no_tiene_id()
        => Assert.Null(AirbnbUrls.IdDeIcal("https://www.airbnb.com/calendar/otra/777.ics"));
}

public class AnuncioInputValidatorTests
{
    private readonly AnuncioInputValidator _v = new();

    private static AnuncioInput Valido() => new()
    {
        Nombre = "Cabaña del lago",
        UrlAnuncio = "https://www.airbnb.com.co/rooms/555",
        IcalUrl = "https://www.airbnb.com/calendar/ical/555.ics?s=secreto"
    };

    [Fact]
    public void Acepta_un_anuncio_con_ical_del_mismo_id()
        => Assert.True(_v.Validate(Valido()).IsValid);

    [Fact]
    public void El_ical_es_opcional()
        => Assert.True(_v.Validate(Valido() with { IcalUrl = null }).IsValid);

    [Fact]
    public void Rechaza_un_ical_de_otro_anuncio()
        => Assert.False(_v.Validate(Valido() with { IcalUrl = "https://www.airbnb.com/calendar/ical/999.ics?s=x" }).IsValid);

    [Theory]
    [InlineData("http://www.airbnb.com/calendar/ical/555.ics")]        // sin https
    [InlineData("https://internal.local/calendar/ical/555.ics")]       // host que no es de Airbnb (SSRF)
    [InlineData("https://www.airbnb.com/calendar/ical/555")]           // no es .ics
    public void Rechaza_enlaces_ical_inseguros_o_mal_formados(string ical)
        => Assert.False(_v.Validate(Valido() with { IcalUrl = ical }).IsValid);

    [Fact]
    public void Rechaza_una_url_de_anuncio_que_no_es_de_airbnb()
        => Assert.False(_v.Validate(Valido() with { UrlAnuncio = "https://example.com/rooms/555" }).IsValid);
}

public class DisponibilidadTests
{
    private static DateOnly D(int dia) => new(2026, 6, dia);

    [Fact]
    public void Fusionar_une_tramos_que_se_tocan_o_solapan_y_recorta_al_rango()
    {
        var r = ObtenerDisponibilidadQueryHandler.Fusionar(
            [(D(10), D(12)), (D(12), D(15)), (D(14), D(16)), (D(20), D(22)), (D(1), D(5))], D(3), D(21));

        Assert.Equal([new RangoOcupadoDto(D(3), D(5)), new RangoOcupadoDto(D(10), D(16)), new RangoOcupadoDto(D(20), D(21))], r);
    }

    [Fact]
    public void Fusionar_descarta_lo_que_cae_fuera_del_rango()
        => Assert.Empty(ObtenerDisponibilidadQueryHandler.Fusionar([(D(1), D(3))], D(3), D(10)));

    [Fact]
    public void Interseccion_deja_solo_las_noches_ocupadas_en_todos_los_anuncios()
    {
        IReadOnlyList<RangoOcupadoDto> a = [new(D(1), D(10)), new(D(20), D(25))];
        IReadOnlyList<RangoOcupadoDto> b = [new(D(5), D(22))];

        var r = ObtenerDisponibilidadQueryHandler.Interseccion([a, b]);

        Assert.Equal([new RangoOcupadoDto(D(5), D(10)), new RangoOcupadoDto(D(20), D(22))], r);
    }

    [Fact]
    public void Interseccion_con_un_solo_anuncio_es_ese_anuncio()
    {
        IReadOnlyList<RangoOcupadoDto> a = [new(D(1), D(3))];

        Assert.Equal(a, ObtenerDisponibilidadQueryHandler.Interseccion([a]));
    }

    [Fact]
    public void Interseccion_sin_solape_es_vacia()
    {
        IReadOnlyList<RangoOcupadoDto> a = [new(D(1), D(3))];
        IReadOnlyList<RangoOcupadoDto> b = [new(D(3), D(6))];

        Assert.Empty(ObtenerDisponibilidadQueryHandler.Interseccion([a, b]));
    }
}
