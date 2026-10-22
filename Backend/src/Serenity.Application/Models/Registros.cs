namespace Serenity.Application.Models;

// Filas tal como salen de la base. Son clases con propiedades init (no records
// posicionales) para que Dapper las hidrate sin exigir tipos exactos en el ctor.

public sealed class MediaRegistro
{
    public long Id { get; init; }
    public string StorageKey { get; init; } = default!;
    public string NombreOriginal { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long TamanoBytes { get; init; }
    public DateTime CreadoEn { get; init; }
}

public sealed class ProyectoDatos
{
    public string Nombre { get; init; } = default!;
    public string Etiqueta { get; init; } = default!;
    public string Icono { get; init; } = default!;
    public string UbicacionEtiqueta { get; init; } = default!;
    public string Titulo { get; init; } = default!;
    public string Descripcion { get; init; } = default!;
    public string MetaLugar { get; init; } = default!;
    public string MetaEstancia { get; init; } = default!;
    public string? UrlReserva { get; init; }
    public string CtaTexto { get; init; } = default!;
    public bool Publicado { get; init; }
}

public sealed class ProyectoRegistro
{
    public long Id { get; init; }
    public string Slug { get; init; } = default!;
    public string Nombre { get; init; } = default!;
    public string Etiqueta { get; init; } = default!;
    public string Icono { get; init; } = default!;
    public string UbicacionEtiqueta { get; init; } = default!;
    public string Titulo { get; init; } = default!;
    public string Descripcion { get; init; } = default!;
    public string MetaLugar { get; init; } = default!;
    public string MetaEstancia { get; init; } = default!;
    public string? UrlReserva { get; init; }
    public string CtaTexto { get; init; } = default!;
    public int Orden { get; init; }
    public bool Publicado { get; init; }
}

public sealed class ProyectoImagenRegistro
{
    public long Id { get; init; }
    public long ProyectoId { get; init; }
    public long MediaId { get; init; }
    public string StorageKey { get; init; } = default!;
    public string TextoAlt { get; init; } = default!;
    public int Orden { get; init; }
}

public sealed class ResenaDatos
{
    public string Autor { get; init; } = default!;
    public string Contexto { get; init; } = default!;
    public string Texto { get; init; } = default!;
    public int Calificacion { get; init; }
    public bool Publicada { get; init; }
}

public sealed class ResenaRegistro
{
    public long Id { get; init; }
    public string Autor { get; init; } = default!;
    public string Contexto { get; init; } = default!;
    public string Texto { get; init; } = default!;
    public int Calificacion { get; init; }
    public int Orden { get; init; }
    public bool Publicada { get; init; }
}

public sealed class ValorDatos
{
    public string Titulo { get; init; } = default!;
    public string Descripcion { get; init; } = default!;
    public string Icono { get; init; } = default!;
    public string Variante { get; init; } = default!;
    public bool Activo { get; init; }
}

public sealed class ValorRegistro
{
    public long Id { get; init; }
    public string Titulo { get; init; } = default!;
    public string Descripcion { get; init; } = default!;
    public string Icono { get; init; } = default!;
    public string Variante { get; init; } = default!;
    public int Orden { get; init; }
    public bool Activo { get; init; }
}

public sealed class SolicitudNueva
{
    public string Nombre { get; init; } = default!;
    public string Contacto { get; init; } = default!;
    public DateOnly FechaLlegada { get; init; }
    public int Huespedes { get; init; }
    public long? ProyectoId { get; init; }
    public string DestinoTexto { get; init; } = default!;
}

public sealed class SolicitudRegistro
{
    public long Id { get; init; }
    public string Nombre { get; init; } = default!;
    public string Contacto { get; init; } = default!;
    public DateOnly FechaLlegada { get; init; }
    public int Huespedes { get; init; }
    public long? ProyectoId { get; init; }
    public string DestinoTexto { get; init; } = default!;
    public string Estado { get; init; } = default!;
    public string? Notas { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime ActualizadoEn { get; init; }
}

public sealed class DashboardDto
{
    public int SolicitudesNuevas { get; init; }
    public int SolicitudesTotal { get; init; }
    public int ProyectosPublicados { get; init; }
    public int ProyectosTotal { get; init; }
    public int ResenasPublicadas { get; init; }
    public int ValoresActivos { get; init; }
    public int ReservasPendientesPago { get; init; }
    public int ReservasEnRevision { get; init; }
    public int ReservasProximas { get; init; }
}
