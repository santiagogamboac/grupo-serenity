using Serenity.Application.Common;
using Serenity.Application.Models;
using Serenity.Domain.Entities;

namespace Serenity.Application.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct);
    Task<Usuario?> ObtenerPorIdAsync(long id, CancellationToken ct);
    Task<long> CrearAsync(Usuario usuario, CancellationToken ct);
    Task<int> ContarAsync(CancellationToken ct);
    Task GuardarBloqueoAsync(Usuario usuario, CancellationToken ct);
    Task ActualizarPasswordAsync(long usuarioId, string passwordHash, CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task GuardarAsync(long usuarioId, string tokenHash, DateTime expiraEnUtc, CancellationToken ct);

    /// <summary>Marca el token como usado de forma atómica y devuelve su dueño; null si no es válido.</summary>
    Task<long?> ConsumirAsync(string tokenHash, CancellationToken ct);

    Task RevocarTodosAsync(long usuarioId, CancellationToken ct);
}

public interface IMediaRepository
{
    Task<long> CrearAsync(MediaRegistro media, CancellationToken ct);
    Task<MediaRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<MediaRegistro>> ObtenerPorIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
    Task<(IReadOnlyList<MediaRegistro> Items, int Total)> ListarAsync(PaginationParams p, CancellationToken ct);
    Task<bool> EstaEnUsoAsync(long id, CancellationToken ct);
    Task<bool> EliminarAsync(long id, CancellationToken ct);
}

public interface ISeccionRepository
{
    /// <summary>clave → JSON crudo.</summary>
    Task<IReadOnlyDictionary<string, string>> ObtenerTodasAsync(CancellationToken ct);
    Task GuardarAsync(string clave, string json, CancellationToken ct);
}

public interface IProyectoRepository
{
    Task<IReadOnlyList<ProyectoRegistro>> ListarAsync(bool soloPublicados, CancellationToken ct);
    Task<ProyectoRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<ProyectoImagenRegistro>> ImagenesAsync(IReadOnlyCollection<long> proyectoIds, CancellationToken ct);
    Task<bool> SlugExisteAsync(string slug, long? excluirId, CancellationToken ct);
    Task<long> CrearAsync(ProyectoDatos datos, string slug, CancellationToken ct);
    Task<bool> ActualizarAsync(long id, ProyectoDatos datos, CancellationToken ct);
    Task<bool> EliminarAsync(long id, CancellationToken ct);
    Task ReordenarAsync(IReadOnlyList<long> ids, CancellationToken ct);
    Task<long> AgregarImagenAsync(long proyectoId, long mediaId, string textoAlt, CancellationToken ct);
    Task<bool> ActualizarAltImagenAsync(long proyectoId, long imagenId, string textoAlt, CancellationToken ct);
    Task<bool> EliminarImagenAsync(long proyectoId, long imagenId, CancellationToken ct);
    Task ReordenarImagenesAsync(long proyectoId, IReadOnlyList<long> ids, CancellationToken ct);
}

public interface IResenaRepository
{
    Task<IReadOnlyList<ResenaRegistro>> ListarAsync(bool soloPublicadas, CancellationToken ct);
    Task<ResenaRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<long> CrearAsync(ResenaDatos datos, CancellationToken ct);
    Task<bool> ActualizarAsync(long id, ResenaDatos datos, CancellationToken ct);
    Task<bool> EliminarAsync(long id, CancellationToken ct);
    Task ReordenarAsync(IReadOnlyList<long> ids, CancellationToken ct);
}

public interface IValorRepository
{
    Task<IReadOnlyList<ValorRegistro>> ListarAsync(bool soloActivos, CancellationToken ct);
    Task<ValorRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<long> CrearAsync(ValorDatos datos, CancellationToken ct);
    Task<bool> ActualizarAsync(long id, ValorDatos datos, CancellationToken ct);
    Task<bool> EliminarAsync(long id, CancellationToken ct);
    Task ReordenarAsync(IReadOnlyList<long> ids, CancellationToken ct);
}

public interface ISolicitudRepository
{
    Task<long> CrearAsync(SolicitudNueva solicitud, CancellationToken ct);
    Task<(IReadOnlyList<SolicitudRegistro> Items, int Total)> ListarAsync(
        string? estado, string? busqueda, PaginationParams p, CancellationToken ct);
    Task<SolicitudRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<bool> ActualizarAsync(long id, string estado, string? notas, CancellationToken ct);
    Task<bool> EliminarAsync(long id, CancellationToken ct);
}

public interface IDashboardRepository
{
    Task<DashboardDto> ObtenerAsync(CancellationToken ct);
}

/// <summary>Banderas internas (p. ej. "el seed inicial ya corrió").</summary>
public interface IEstadoAppRepository
{
    Task<bool> ExisteAsync(string clave, CancellationToken ct);
    Task MarcarAsync(string clave, string valor, CancellationToken ct);
}
