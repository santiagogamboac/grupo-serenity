using System.Text.Json;
using System.Text.Json.Serialization;
using Serenity.Application.Features.Media;

namespace Serenity.Application.Features.Contenido;

/// <summary>
/// Una sección editable de la landing. Se guarda como JSONB. Las imágenes se guardan
/// por id (<c>xxxMediaId</c>) y en las lecturas se expanden a <see cref="MediaDto"/>.
/// </summary>
public interface ISeccionContenido
{
    IEnumerable<long> MediaIds();

    /// <summary>Copia con las imágenes expandidas desde el diccionario.</summary>
    ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> media);

    /// <summary>Copia sin los campos derivados (lo que se persiste).</summary>
    ISeccionContenido ParaGuardar();

    /// <summary>Copia sin ids internos (lo que ve el público).</summary>
    ISeccionContenido ParaPublico();
}

public sealed record EnlaceNavegacion(string Texto, string Href);

public sealed record MarcaContenido : ISeccionContenido
{
    public string Nombre { get; init; } = "";
    public string TituloSitio { get; init; } = "";
    public string Leyenda { get; init; } = "";
    public string LogoAlt { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? LogoMediaId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? PajaroMediaId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MediaDto? Logo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MediaDto? Pajaro { get; init; }

    public IEnumerable<long> MediaIds() => new[] { LogoMediaId, PajaroMediaId }.OfType<long>();
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this with
    {
        Logo = SeccionesRegistro.Buscar(m, LogoMediaId),
        Pajaro = SeccionesRegistro.Buscar(m, PajaroMediaId)
    };
    public ISeccionContenido ParaGuardar() => this with { Logo = null, Pajaro = null };
    public ISeccionContenido ParaPublico() => this with { LogoMediaId = null, PajaroMediaId = null };
}

public sealed record NavegacionContenido : ISeccionContenido
{
    public IReadOnlyList<EnlaceNavegacion> Items { get; init; } = [];
    public string CtaTexto { get; init; } = "";
    public string CtaHref { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record AnuncioContenido : ISeccionContenido
{
    public bool Activo { get; init; }
    public string Texto { get; init; } = "";
    public string CtaTexto { get; init; } = "";
    public string CtaHref { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record HeroContenido : ISeccionContenido
{
    public string Kicker { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Texto { get; init; } = "";
    public string CtaPrimarioTexto { get; init; } = "";
    public string CtaPrimarioHref { get; init; } = "";
    public string CtaSecundarioTexto { get; init; } = "";
    public string CtaSecundarioHref { get; init; } = "";
    public string ImagenPrincipalAlt { get; init; } = "";
    public string ImagenSecundariaAlt { get; init; } = "";
    public string NotaDestinos { get; init; } = "";
    public string SelloLinea1 { get; init; } = "";
    public string SelloCentral { get; init; } = "";
    public string SelloLinea3 { get; init; } = "";
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ImagenPrincipalMediaId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public long? ImagenSecundariaMediaId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MediaDto? ImagenPrincipal { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public MediaDto? ImagenSecundaria { get; init; }

    public IEnumerable<long> MediaIds() => new[] { ImagenPrincipalMediaId, ImagenSecundariaMediaId }.OfType<long>();
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this with
    {
        ImagenPrincipal = SeccionesRegistro.Buscar(m, ImagenPrincipalMediaId),
        ImagenSecundaria = SeccionesRegistro.Buscar(m, ImagenSecundariaMediaId)
    };
    public ISeccionContenido ParaGuardar() => this with { ImagenPrincipal = null, ImagenSecundaria = null };
    public ISeccionContenido ParaPublico() => this with { ImagenPrincipalMediaId = null, ImagenSecundariaMediaId = null };
}

public sealed record IntroContenido : ISeccionContenido
{
    public string Etiqueta { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Lead { get; init; } = "";
    public string Texto { get; init; } = "";
    public string EnlaceTexto { get; init; } = "";
    public string EnlaceHref { get; init; } = "";
    public string Cita { get; init; } = "";
    public string Firma { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record ValoresContenido : ISeccionContenido
{
    public string Etiqueta { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Texto { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record DestinosContenido : ISeccionContenido
{
    public string Etiqueta { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Texto { get; init; } = "";
    public string FranjaEtiqueta { get; init; } = "";
    public string FranjaTitulo { get; init; } = "";
    public string ChipMarca { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record ResenasContenido : ISeccionContenido
{
    public string Etiqueta { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Calificacion { get; init; } = "";
    public string Detalle { get; init; } = "";
    public string EnlaceTexto { get; init; } = "";
    public string EnlaceUrl { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record ReservasContenido : ISeccionContenido
{
    public string Etiqueta { get; init; } = "";
    public string Titulo { get; init; } = "";
    public string Texto { get; init; } = "";
    public string WhatsappTexto { get; init; } = "";
    public string WhatsappUrl { get; init; } = "";
    public string Nota { get; init; } = "";
    public string FormTitulo { get; init; } = "";
    public string FormSubtitulo { get; init; } = "";
    public string BotonTexto { get; init; } = "";
    public string Aviso { get; init; } = "";
    public string ExitoTitulo { get; init; } = "";
    public string ExitoDescripcion { get; init; } = "";

    // Reserva en línea (alojamientos con precio y pago con Wompi).
    public string ReservaTitulo { get; init; } = "";
    public string ReservaSubtitulo { get; init; } = "";
    public string ReservaBotonTexto { get; init; } = "";
    public string Condiciones { get; init; } = "";
    public string ReservaSinPagoTexto { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

public sealed record FooterContenido : ISeccionContenido
{
    public string Eslogan { get; init; } = "";
    public string RedesTitulo { get; init; } = "";
    public string FacebookUrl { get; init; } = "";
    public string InstagramUrl { get; init; } = "";
    public string YoutubeUrl { get; init; } = "";
    public string Copyright { get; init; } = "";
    public string Frase { get; init; } = "";

    public IEnumerable<long> MediaIds() => [];
    public ISeccionContenido ConMedia(IReadOnlyDictionary<long, MediaDto> m) => this;
    public ISeccionContenido ParaGuardar() => this;
    public ISeccionContenido ParaPublico() => this;
}

/// <summary>Catálogo de secciones: clave → tipo, y (de)serialización JSON única.</summary>
public static class SeccionesRegistro
{
    public static readonly IReadOnlyDictionary<string, Type> Tipos = new Dictionary<string, Type>
    {
        ["marca"] = typeof(MarcaContenido),
        ["navegacion"] = typeof(NavegacionContenido),
        ["anuncio"] = typeof(AnuncioContenido),
        ["hero"] = typeof(HeroContenido),
        ["intro"] = typeof(IntroContenido),
        ["valores"] = typeof(ValoresContenido),
        ["destinos"] = typeof(DestinosContenido),
        ["resenas"] = typeof(ResenasContenido),
        ["reservas"] = typeof(ReservasContenido),
        ["footer"] = typeof(FooterContenido)
    };

    public static IReadOnlyCollection<string> Claves => Tipos.Keys.ToList();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static MediaDto? Buscar(IReadOnlyDictionary<long, MediaDto> m, long? id)
        => id is { } i && m.TryGetValue(i, out var dto) ? dto : null;

    public static string Serializar(ISeccionContenido seccion)
        => JsonSerializer.Serialize(seccion.ParaGuardar(), seccion.GetType(), Json);

    /// <summary>Deserializa el cuerpo de un PUT. Devuelve null si la clave no existe o el JSON no encaja.</summary>
    public static ISeccionContenido? Deserializar(string clave, JsonElement cuerpo, out string? error)
    {
        error = null;
        if (!Tipos.TryGetValue(clave, out var tipo))
        {
            error = $"La sección '{clave}' no existe.";
            return null;
        }

        try
        {
            return cuerpo.Deserialize(tipo, Json) as ISeccionContenido
                   ?? throw new JsonException("Cuerpo vacío.");
        }
        catch (JsonException ex)
        {
            error = $"JSON inválido para la sección '{clave}': {ex.Message}";
            return null;
        }
    }

    /// <summary>Lee la sección guardada; si falta (o está corrupta) devuelve una vacía del tipo correcto.</summary>
    public static ISeccionContenido DeserializarOVacia(string clave, string? json)
    {
        var tipo = Tipos[clave];
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonSerializer.Deserialize(json, tipo, Json) is ISeccionContenido s)
                {
                    return s;
                }
            }
            catch (JsonException)
            {
                // Sección corrupta: se cae a la vacía en vez de romper toda la landing.
            }
        }

        return (ISeccionContenido)Activator.CreateInstance(tipo)!;
    }
}
