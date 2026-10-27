using System.Text.Json;
using Serenity.Application.Common;
using Serenity.Application.Features.Auth;
using Serenity.Application.Features.Contenido;
using Serenity.Application.Features.Media;
using Serenity.Application.Features.Proyectos;
using Serenity.Application.Features.Solicitudes;

namespace Serenity.Tests;

public class SlugGeneratorTests
{
    [Theory]
    [InlineData("Santa Marta", "santa-marta")]
    [InlineData("La Dorada, Caldas", "la-dorada-caldas")]
    [InlineData("  Montaña — Reserva del Mar!! ", "montana-reserva-del-mar")]
    [InlineData("Ñandú & Café", "nandu-cafe")]
    [InlineData("!!!", "proyecto")]
    public void Genera_slugs_limpios(string entrada, string esperado)
        => Assert.Equal(esperado, SlugGenerator.Generar(entrada));

    [Fact]
    public void Limita_la_longitud()
        => Assert.True(SlugGenerator.Generar(new string('a', 400)).Length <= 150);
}

public class ReglasMediaTests
{
    [Fact]
    public void Detecta_png_jpeg_gif_webp_por_firma()
    {
        Assert.Equal("image/png", ReglasMedia.DetectarTipo([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])?.ContentType);
        Assert.Equal("image/jpeg", ReglasMedia.DetectarTipo([0xFF, 0xD8, 0xFF, 0xE0])?.ContentType);
        Assert.Equal("image/gif", ReglasMedia.DetectarTipo("GIF89a"u8)?.ContentType);
        Assert.Equal("image/webp", ReglasMedia.DetectarTipo("RIFF\0\0\0\0WEBP"u8)?.ContentType);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("MZ\u0090\0\u0003")]
    [InlineData("texto plano")]
    [InlineData("")]
    public void Rechaza_lo_que_no_es_imagen_soportada(string contenido)
        => Assert.Null(ReglasMedia.DetectarTipo(System.Text.Encoding.Latin1.GetBytes(contenido)));
}

public class OrdenamientoTests
{
    [Fact]
    public void Permutacion_valida() => Assert.True(Ordenamiento.EsPermutacion([3, 1, 2], [1, 2, 3]));

    [Theory]
    [InlineData(new long[] { 1, 2 })]
    [InlineData(new long[] { 1, 1, 2 })]
    [InlineData(new long[] { 1, 2, 4 })]
    [InlineData(new long[] { 1, 2, 3, 4 })]
    public void Permutacion_invalida(long[] ids) => Assert.False(Ordenamiento.EsPermutacion(ids, [1, 2, 3]));

    [Fact]
    public void Null_no_es_permutacion() => Assert.False(Ordenamiento.EsPermutacion(null, [1]));
}

public class EnlaceSeguroTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("#reservas")]
    [InlineData("/admin")]
    [InlineData("https://www.airbnb.com.co/rooms/12345")]
    [InlineData("http://wa.link/57cc3y")]
    [InlineData("mailto:hola@serenity.co")]
    [InlineData("tel:+573001112233")]
    public void Acepta(string? valor) => Assert.True(ValidacionExtensions.EsEnlaceSeguro(valor));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("//evil.com")]
    [InlineData("ftp://x.com")]
    [InlineData("no es una url")]
    public void Rechaza(string valor) => Assert.False(ValidacionExtensions.EsEnlaceSeguro(valor));
}

public class SolicitudValidacionTests
{
    private sealed class Reloj(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    // 2026-06-10 02:00 UTC = 2026-06-09 21:00 en Colombia (UTC-5).
    private static readonly TimeProvider RelojFijo = new Reloj(new DateTimeOffset(2026, 6, 10, 2, 0, 0, TimeSpan.Zero));

    private static SolicitudInput Valida() => new()
    {
        Nombre = "Ana", Contacto = "ana@example.com", FechaLlegada = new DateOnly(2026, 7, 1),
        Huespedes = 2, DestinoTexto = "Santa Marta"
    };

    private static bool EsValida(SolicitudInput i)
        => new CrearSolicitudCommandValidator(RelojFijo).Validate(new CrearSolicitudCommand(i)).IsValid;

    [Theory]
    [InlineData("ana@example.com", true)]
    [InlineData("+57 300 111 2233", true)]
    [InlineData("300-111-2233", true)]
    [InlineData("(601) 555 1234", true)]
    [InlineData("no-es-nada", false)]
    [InlineData("123", false)]
    [InlineData("a@b", false)]
    [InlineData("", false)]
    public void Contacto(string contacto, bool esperado)
        => Assert.Equal(esperado, CrearSolicitudCommandValidator.EsContactoValido(contacto));

    [Fact]
    public void Solicitud_valida_pasa() => Assert.True(EsValida(Valida()));

    [Fact]
    public void Hoy_en_Colombia_es_valido_aunque_en_UTC_ya_sea_manana()
        => Assert.True(EsValida(Valida() with { FechaLlegada = new DateOnly(2026, 6, 9) }));

    [Fact]
    public void Ayer_en_Colombia_no_es_valido()
        => Assert.False(EsValida(Valida() with { FechaLlegada = new DateOnly(2026, 6, 8) }));

    [Fact]
    public void Fecha_demasiado_lejana_no_es_valida()
        => Assert.False(EsValida(Valida() with { FechaLlegada = new DateOnly(2030, 1, 1) }));

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Huespedes_fuera_de_rango(int n) => Assert.False(EsValida(Valida() with { Huespedes = n }));

    [Fact]
    public void Requiere_destino()
        => Assert.False(EsValida(Valida() with { DestinoTexto = "  ", ProyectoId = null }));

    [Fact]
    public void Proyecto_id_sustituye_al_texto()
        => Assert.True(EsValida(Valida() with { DestinoTexto = null, ProyectoId = 3 }));
}

public class PasswordValidacionTests
{
    private static bool Valida(string nueva, string actual = "Actual-Clave-1")
        => new CambiarPasswordCommandValidator().Validate(new CambiarPasswordCommand(1, actual, nueva)).IsValid;

    [Theory]
    [InlineData("Nueva-Clave-2026", true)]
    [InlineData("corta1", false)]
    [InlineData("sololetrasperolarga", false)]
    [InlineData("12345678901234", false)]
    public void Politica(string nueva, bool esperado) => Assert.Equal(esperado, Valida(nueva));

    [Fact]
    public void Debe_ser_distinta_de_la_actual() => Assert.False(Valida("Actual-Clave-1"));
}

public class SeccionesTests
{
    [Fact]
    public void ParaGuardar_no_persiste_los_campos_derivados()
    {
        var hero = new HeroContenido
        {
            Kicker = "k",
            ImagenPrincipalMediaId = 7,
            ImagenPrincipal = new MediaDto(7, "/media/x.jpg", "x.jpg")
        };

        var json = SeccionesRegistro.Serializar(hero);

        Assert.Contains("\"imagenPrincipalMediaId\":7", json);
        Assert.DoesNotContain("imagenPrincipal\"", json);
    }

    [Fact]
    public void ParaPublico_oculta_ids_internos()
    {
        var marca = new MarcaContenido { LogoMediaId = 1, Logo = new MediaDto(1, "/media/l.png", "l.png") };

        var publico = JsonSerializer.Serialize((object)marca.ParaPublico(), SeccionesRegistro.Json);

        Assert.DoesNotContain("logoMediaId", publico, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"logo\"", publico);
    }

    [Fact]
    public void ConMedia_expande_las_imagenes()
    {
        var hero = new HeroContenido { ImagenPrincipalMediaId = 5 };
        var conMedia = (HeroContenido)hero.ConMedia(new Dictionary<long, MediaDto> { [5] = new(5, "/media/a.jpg", "a.jpg") });

        Assert.Equal("/media/a.jpg", conMedia.ImagenPrincipal?.Url);
        Assert.Null(conMedia.ImagenSecundaria);
    }

    [Fact]
    public void Deserializa_por_clave_y_rechaza_desconocidas()
    {
        var cuerpo = JsonDocument.Parse("""{"kicker":"Hola"}""").RootElement;

        Assert.Equal("Hola", ((HeroContenido)SeccionesRegistro.Deserializar("hero", cuerpo, out _)!).Kicker);
        Assert.Null(SeccionesRegistro.Deserializar("nada", cuerpo, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Seccion_corrupta_cae_a_vacia_sin_romper()
        => Assert.IsType<HeroContenido>(SeccionesRegistro.DeserializarOVacia("hero", "{esto no es json"));

    [Fact]
    public void Hero_vacio_no_pasa_validacion()
        => Assert.False(new HeroValidator().Validate(new HeroContenido()).IsValid);

    [Fact]
    public void Navegacion_exige_entre_1_y_8_enlaces()
    {
        var ok = new NavegacionContenido { Items = [new("Inicio", "#inicio")], CtaTexto = "Reservar", CtaHref = "#reservas" };
        Assert.True(new NavegacionValidator().Validate(ok).IsValid);
        Assert.False(new NavegacionValidator().Validate(ok with { Items = [] }).IsValid);
        Assert.False(new NavegacionValidator().Validate(ok with { Items = [new("x", "javascript:1")] }).IsValid);
    }
}

public class ProyectoValidacionTests
{
    private static ProyectoInput Valido() => new()
    {
        Nombre = "Santa Marta", Etiqueta = "Playa", Icono = "waves", UbicacionEtiqueta = "Santa Marta",
        Titulo = "El mar cerca.", Descripcion = "Texto", CtaTexto = "Ver"
    };

    [Fact]
    public void Valido_pasa() => Assert.True(new ProyectoInputValidator().Validate(Valido()).IsValid);

    [Fact]
    public void Icono_fuera_del_catalogo_falla()
        => Assert.False(new ProyectoInputValidator().Validate(Valido() with { Icono = "nope" }).IsValid);

    [Fact]
    public void Url_peligrosa_falla()
        => Assert.False(new ProyectoInputValidator().Validate(Valido() with { UrlReserva = "javascript:alert(1)" }).IsValid);
}
