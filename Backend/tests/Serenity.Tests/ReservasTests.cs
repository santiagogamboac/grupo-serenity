using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serenity.Application.Common;
using Serenity.Application.Features.Reservas;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;

namespace Serenity.Tests;

public class CotizadorTests
{
    private static readonly DateOnly Hoy = new(2027, 1, 10);

    private static AlojamientoRegistro Cabana(int capacidad = 4, int incluidos = 2, long precio = 300_000, long extra = 50_000, long aseo = 80_000, int minima = 2) => new()
    {
        Id = 7, ProyectoId = 1, Nombre = "Cabaña del lago", Descripcion = "", Capacidad = capacidad, HuespedesIncluidos = incluidos,
        PrecioNoche = precio, RecargoHuespedExtra = extra, TarifaAseo = aseo, EstanciaMinima = minima, TokenIcal = "x", Publicado = true
    };

    private static TarifaRegistro Temporada(DateOnly desde, DateOnly hasta, long precio, string nombre = "Temporada alta")
        => new() { Id = 1, AlojamientoId = 7, Nombre = nombre, Desde = desde, Hasta = hasta, PrecioNoche = precio };

    [Fact]
    public void Precio_base_mas_aseo_sin_huespedes_extra()
    {
        var r = Cotizador.Cotizar(Cabana(), [], new(2027, 2, 1), new(2027, 2, 4), 2, Hoy);

        Assert.True(r.IsSuccess);
        var c = r.Value!;
        Assert.Equal(3, c.Noches);
        Assert.Equal(900_000, c.Subtotal);
        Assert.Equal(80_000, c.TarifaAseo);
        Assert.Equal(980_000, c.Total);
        var tramo = Assert.Single(c.Tramos);
        Assert.Null(tramo.Temporada);
    }

    [Fact]
    public void Huespedes_extra_pagan_recargo_por_noche()
    {
        var c = Cotizador.Cotizar(Cabana(), [], new(2027, 2, 1), new(2027, 2, 3), 4, Hoy).Value!;

        Assert.Equal(2, c.HuespedesExtra);
        Assert.Equal(2 * 50_000 * 2, c.RecargoHuespedes);
        Assert.Equal(600_000 + 200_000, c.Subtotal);
        Assert.Equal(c.Subtotal + c.TarifaAseo, c.Total);
    }

    [Fact]
    public void Temporada_aplica_solo_a_las_noches_que_cubre_y_parte_los_tramos()
    {
        // Temporada [3 feb, 5 feb): noches del 3 y del 4.
        var tarifas = new[] { Temporada(new(2027, 2, 3), new(2027, 2, 5), 450_000) };

        var c = Cotizador.Cotizar(Cabana(), tarifas, new(2027, 2, 1), new(2027, 2, 6), 2, Hoy).Value!;

        Assert.Equal(5, c.Noches);
        Assert.Collection(c.Tramos,
            t => { Assert.Equal(2, t.Noches); Assert.Equal(300_000, t.PrecioNoche); Assert.Null(t.Temporada); },
            t => { Assert.Equal(2, t.Noches); Assert.Equal(450_000, t.PrecioNoche); Assert.Equal("Temporada alta", t.Temporada); Assert.Equal(new DateOnly(2027, 2, 5), t.Hasta); },
            t => { Assert.Equal(1, t.Noches); Assert.Equal(300_000, t.PrecioNoche); });
        Assert.Equal(3 * 300_000 + 2 * 450_000, c.Subtotal);
    }

    [Theory]
    [InlineData(2027, 1, 9, 2027, 1, 12, 2, "pasado")]
    [InlineData(2027, 2, 1, 2027, 2, 1, 2, "posterior")]
    [InlineData(2027, 2, 1, 2027, 2, 2, 2, "mínima")]
    [InlineData(2027, 2, 1, 2027, 2, 4, 5, "admite")]
    [InlineData(2027, 2, 1, 2027, 4, 15, 2, "hasta 60 noches")]
    [InlineData(2029, 2, 1, 2029, 2, 4, 2, "lejana")]
    public void Rechaza_estancias_invalidas(int y1, int m1, int d1, int y2, int m2, int d2, int huespedes, string fragmento)
    {
        var r = Cotizador.Cotizar(Cabana(), [], new(y1, m1, d1), new(y2, m2, d2), huespedes, Hoy);

        Assert.True(r.IsFailure);
        Assert.Contains(fragmento, r.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Se_puede_llegar_hoy()
        => Assert.True(Cotizador.Cotizar(Cabana(), [], Hoy, Hoy.AddDays(2), 1, Hoy).IsSuccess);

    [Fact]
    public void El_dia_de_salida_de_una_reserva_puede_ser_el_de_llegada_de_otra()
    {
        var ocupado = new[] { new OcupacionRegistro { Desde = new(2027, 2, 1), Hasta = new(2027, 2, 4), Origen = "reserva", Referencia = "SER-A" } };

        Assert.False(Cotizador.SeCruza(ocupado, new(2027, 2, 4), new(2027, 2, 6)));
        Assert.False(Cotizador.SeCruza(ocupado, new(2027, 1, 29), new(2027, 2, 1)));
        Assert.True(Cotizador.SeCruza(ocupado, new(2027, 2, 3), new(2027, 2, 5)));
        Assert.True(Cotizador.SeCruza(ocupado, new(2027, 1, 30), new(2027, 2, 10)));
    }
}

public class WompiTests
{
    [Fact]
    public void Firma_de_integridad_coincide_con_el_ejemplo_de_la_documentacion()
        => Assert.Equal(
            "37c8407747e595535433ef8f6a811d853cd943046624a0ec04662b17bbf33bf5",
            Wompi.FirmaIntegridad("sk8-438k4-xmxm392-sn2m", 2490000, "COP", null, "prod_integrity_Z5mMke9x0k8gpErbDqwrJXMqsI6SFli6"));

    [Fact]
    public void Con_expiracion_la_fecha_va_antes_del_secreto()
    {
        var esperado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "ref-1" + "100000" + "COP" + "2023-06-09T20:28:50.000Z" + "secreto"))).ToLowerInvariant();

        Assert.Equal(esperado, Wompi.FirmaIntegridad("ref-1", 100000, "COP", "2023-06-09T20:28:50.000Z", "secreto"));
        Assert.Equal("2023-06-09T20:28:50.000Z", Wompi.FormatoExpiracion(new DateTime(2023, 6, 9, 20, 28, 50, DateTimeKind.Utc)));
    }

    [Fact]
    public void Url_de_checkout_lleva_centavos_referencia_firma_y_regreso()
    {
        var o = new OpcionesWompi { LlavePublica = "pub_test_abc", SecretoIntegridad = "test_integrity_x", SecretoEventos = "e" };
        var r = new ReservaRegistro
        {
            Codigo = "SER-ABC234", Total = 980_000, Moneda = "COP", Correo = "ana@example.com", Nombre = "Ana Gómez",
            ExpiraEn = new DateTime(2027, 1, 10, 15, 0, 0, DateTimeKind.Utc)
        };

        var url = new Uri(Wompi.ConstruirUrlCheckout(o, r, "SER-ABC234-1", "https://serenity.test/reserva/SER-ABC234?t=tok"));
        var q = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("checkout.wompi.co", url.Host);
        Assert.Equal("98000000", q["amount-in-cents"]);
        Assert.Equal("SER-ABC234-1", q["reference"]);
        Assert.Equal("https://serenity.test/reserva/SER-ABC234?t=tok", q["redirect-url"]);
        Assert.Equal("2027-01-10T15:00:00.000Z", q["expiration-time"]);
        Assert.Equal("Ana Gómez", q["customer-data:full-name"]);
        Assert.Equal(Wompi.FirmaIntegridad("SER-ABC234-1", 98000000, "COP", "2027-01-10T15:00:00.000Z", "test_integrity_x"), q["signature:integrity"]);
    }

    [Theory]
    [InlineData("SER-ABC234-1", "SER-ABC234")]
    [InlineData("SER-ABC234-12", "SER-ABC234")]
    [InlineData("SER-ABC234", null)]
    [InlineData("otra-cosa-x", null)]
    [InlineData("", null)]
    public void Codigo_de_referencia(string referencia, string? esperado)
        => Assert.Equal(esperado, Wompi.CodigoDeReferencia(referencia));

    private const string SecretoEventos = "test_events_secreto";

    private static string Evento(string estado = "APPROVED", long monto = 98000000, string? checksum = null, long timestamp = 1530291411)
    {
        var firma = checksum ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"1234-1610641025-49201{estado}{monto}{timestamp}{SecretoEventos}")));
        return $$"""
        {
          "event": "transaction.updated",
          "data": { "transaction": { "id": "1234-1610641025-49201", "amount_in_cents": {{monto}}, "reference": "SER-ABC234-1",
                    "customer_email": "ana@example.com", "currency": "COP", "payment_method_type": "NEQUI", "status": "{{estado}}" } },
          "environment": "test",
          "signature": { "properties": ["transaction.id", "transaction.status", "transaction.amount_in_cents"], "checksum": "{{firma}}" },
          "timestamp": {{timestamp}},
          "sent_at": "2018-07-20T16:45:05.000Z"
        }
        """;
    }

    [Fact]
    public void Evento_con_checksum_correcto_devuelve_la_transaccion()
    {
        var tx = Wompi.LeerEventoValido(JsonDocument.Parse(Evento()).RootElement, SecretoEventos);

        Assert.NotNull(tx);
        Assert.Equal("1234-1610641025-49201", tx.Id);
        Assert.Equal("SER-ABC234-1", tx.Referencia);
        Assert.Equal("APPROVED", tx.Estado);
        Assert.Equal(98000000, tx.MontoCentavos);
        Assert.Equal("NEQUI", tx.Metodo);
    }

    [Fact]
    public void Evento_alterado_o_con_otro_secreto_se_rechaza()
    {
        var valido = Evento();
        var alterado = valido.Replace("\"amount_in_cents\": 98000000", "\"amount_in_cents\": 100");

        Assert.Null(Wompi.LeerEventoValido(JsonDocument.Parse(alterado).RootElement, SecretoEventos));
        Assert.Null(Wompi.LeerEventoValido(JsonDocument.Parse(valido).RootElement, "otro_secreto"));
        Assert.Null(Wompi.LeerEventoValido(JsonDocument.Parse(Evento(checksum: "abc")).RootElement, SecretoEventos));
        Assert.Null(Wompi.LeerEventoValido(JsonDocument.Parse("{\"event\":\"x\"}").RootElement, SecretoEventos));
    }
}

public class IcalWriterTests
{
    [Fact]
    public void Escribe_eventos_de_dia_completo_que_el_parser_vuelve_a_leer()
    {
        var a = new AlojamientoRegistro { Id = 3, Nombre = "Casa, del río; norte", TokenIcal = "t" };
        var ocupado = new[]
        {
            new OcupacionAlojamiento { AlojamientoId = 3, Desde = new(2027, 3, 1), Hasta = new(2027, 3, 4), Origen = "reserva", Referencia = "SER-AAA222" },
            new OcupacionAlojamiento { AlojamientoId = 3, Desde = new(2027, 3, 10), Hasta = new(2027, 3, 11), Origen = "manual", Referencia = "9" }
        };

        var ics = IcalWriter.Escribir(a, ocupado, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Contains("X-WR-CALNAME:Casa\\, del río\\; norte\r\n", ics);
        Assert.Contains("UID:reserva-SER-AAA222-3@gruposerenity", ics);
        var leidos = IcalParser.LeerBloqueos(ics);
        Assert.Equal(2, leidos.Count);
        Assert.Equal(new DateOnly(2027, 3, 4), leidos[0].Hasta);
        Assert.Equal(new DateOnly(2027, 3, 10), leidos[1].Desde);
    }
}

public class FormatoColombiaTests
{
    [Theory]
    [InlineData(0, "$0")]
    [InlineData(950, "$950")]
    [InlineData(1_250_000, "$1.250.000")]
    public void Pesos_con_punto_de_miles(long valor, string esperado) => Assert.Equal(esperado, FormatoColombia.Pesos(valor));

    [Fact]
    public void Fecha_larga_en_espanol() => Assert.Equal("viernes 15 de enero de 2027", FormatoColombia.Fecha(new DateOnly(2027, 1, 15)));

    [Fact]
    public void Hoy_usa_utc_menos_5()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2027, 1, 16, 3, 0, 0, TimeSpan.Zero)); // 15 ene, 10 p. m. en Bogotá
        Assert.Equal(new DateOnly(2027, 1, 15), FormatoColombia.Hoy(reloj));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
