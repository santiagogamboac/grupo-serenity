using Serenity.Application.Common;

namespace Serenity.Application.Models;

public sealed class AnuncioDatos
{
    public string Nombre { get; init; } = default!;
    public string AirbnbRoomId { get; init; } = default!;
    public string UrlAnuncio { get; init; } = default!;
    public string? IcalUrl { get; init; }
    public bool Publicado { get; init; }
}

public sealed class AnuncioRegistro
{
    public long Id { get; init; }
    public long ProyectoId { get; init; }
    public string Nombre { get; init; } = default!;
    public string AirbnbRoomId { get; init; } = default!;
    public string UrlAnuncio { get; init; } = default!;
    public string? IcalUrl { get; init; }
    public int Orden { get; init; }
    public bool Publicado { get; init; }
    public DateTime? UltimaSyncIntento { get; init; }
    public DateTime? UltimaSyncOk { get; init; }
    public string? UltimaSyncError { get; init; }
}

public sealed class BloqueoRegistro
{
    public long AnuncioId { get; init; }
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
}
