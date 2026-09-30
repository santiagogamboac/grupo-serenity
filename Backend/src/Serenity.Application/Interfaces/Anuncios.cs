using Serenity.Application.Common;
using Serenity.Application.Models;

namespace Serenity.Application.Interfaces;

public interface IAnuncioRepository
{
    Task<IReadOnlyList<AnuncioRegistro>> ListarPorProyectoAsync(long proyectoId, bool soloPublicados, CancellationToken ct);

    /// <summary>Anuncios publicados con iCal configurado: los que recorre el worker.</summary>
    Task<IReadOnlyList<AnuncioRegistro>> ListarSincronizablesAsync(CancellationToken ct);

    Task<AnuncioRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<bool> RoomIdExisteAsync(string roomId, long? excluirId, CancellationToken ct);
    Task<long> CrearAsync(long proyectoId, AnuncioDatos datos, CancellationToken ct);

    /// <summary>Si cambia o se quita el iCal, se descartan los bloqueos y el estado de sincronización anteriores.</summary>
    Task<bool> ActualizarAsync(long id, AnuncioDatos datos, CancellationToken ct);

    Task<bool> EliminarAsync(long id, CancellationToken ct);

    /// <summary>Reemplaza en una transacción los bloqueos de origen airbnb del anuncio y marca la sincronización como correcta.</summary>
    Task GuardarSincronizacionAsync(long id, IReadOnlyList<BloqueoIcal> bloqueos, CancellationToken ct);

    /// <summary>Registra el fallo sin tocar los bloqueos existentes.</summary>
    Task GuardarErrorSincronizacionAsync(long id, string error, CancellationToken ct);

    /// <summary>
    /// Reserva de forma atómica el turno de sincronizar: true solo si el último intento tiene más de
    /// <paramref name="minSegundos"/>. Evita que varias visitas (o réplicas) descarguen el mismo .ics a la vez.
    /// </summary>
    Task<bool> TomarTurnoSincronizacionAsync(long id, int minSegundos, CancellationToken ct);

    Task<IReadOnlyList<BloqueoRegistro>> BloqueosAsync(IReadOnlyCollection<long> anuncioIds, DateOnly desde, DateOnly hasta, CancellationToken ct);
}

public interface IIcalClient
{
    /// <summary>Descarga el .ics. Lanza excepción con mensaje legible si falla (red, HTTP, tamaño).</summary>
    Task<string> DescargarAsync(string url, CancellationToken ct);
}
