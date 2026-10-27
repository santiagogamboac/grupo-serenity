using Serenity.Application.Common;
using Serenity.Application.Models;

namespace Serenity.Application.Interfaces;

public interface IAlojamientoRepository
{
    Task<IReadOnlyList<AlojamientoRegistro>> ListarPorProyectoAsync(long proyectoId, bool soloPublicados, CancellationToken ct);

    /// <summary>Alojamientos publicados de proyectos publicados, para la landing.</summary>
    Task<IReadOnlyList<AlojamientoRegistro>> ListarPublicosAsync(CancellationToken ct);

    Task<AlojamientoRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<AlojamientoRegistro?> ObtenerPorTokenIcalAsync(string token, CancellationToken ct);
    Task<long> CrearAsync(long proyectoId, AlojamientoDatos datos, string tokenIcal, CancellationToken ct);
    Task<bool> ActualizarAsync(long id, AlojamientoDatos datos, CancellationToken ct);
    Task<bool> EliminarAsync(long id, CancellationToken ct);
    Task<bool> TieneReservasAsync(long id, CancellationToken ct);
    Task<bool> ProyectoTieneReservasAsync(long proyectoId, CancellationToken ct);

    /// <summary>¿El anuncio de Airbnb ya está vinculado a otro alojamiento?</summary>
    Task<bool> AnuncioVinculadoAsync(long anuncioId, long? excluirId, CancellationToken ct);

    /// <summary>Galería (sin la portada), ordenada por alojamiento y orden.</summary>
    Task<IReadOnlyList<AlojamientoImagenRegistro>> ImagenesAsync(IReadOnlyCollection<long> alojamientoIds, CancellationToken ct);
    Task<long> AgregarImagenAsync(long alojamientoId, long mediaId, string textoAlt, CancellationToken ct);
    Task<bool> ActualizarAltImagenAsync(long alojamientoId, long imagenId, string textoAlt, CancellationToken ct);
    Task<bool> EliminarImagenAsync(long alojamientoId, long imagenId, CancellationToken ct);
    Task ReordenarImagenesAsync(long alojamientoId, IReadOnlyList<long> ids, CancellationToken ct);

    Task<IReadOnlyList<TarifaRegistro>> TarifasAsync(IReadOnlyCollection<long> alojamientoIds, CancellationToken ct);

    /// <summary>null si se solapa con otra temporada del mismo alojamiento.</summary>
    Task<long?> CrearTarifaAsync(long alojamientoId, string nombre, DateOnly desde, DateOnly hasta, long precioNoche, CancellationToken ct);

    Task<bool> EliminarTarifaAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<BloqueoManualRegistro>> BloqueosManualesAsync(long alojamientoId, CancellationToken ct);
    Task<long> CrearBloqueoAsync(long alojamientoId, DateOnly desde, DateOnly hasta, string motivo, CancellationToken ct);
    Task<bool> EliminarBloqueoAsync(long id, CancellationToken ct);

    /// <summary>
    /// Noches ocupadas que tocan [desde, hasta): reservas vivas, bloqueos manuales y bloqueos de Airbnb del anuncio vinculado.
    /// </summary>
    Task<IReadOnlyList<OcupacionAlojamiento>> OcupacionAsync(
        IReadOnlyCollection<long> alojamientoIds, DateOnly desde, DateOnly hasta, CancellationToken ct);
}

public sealed class OcupacionAlojamiento
{
    public long AlojamientoId { get; init; }
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    public string Origen { get; init; } = default!;
    public string Referencia { get; init; } = default!;

    public OcupacionRegistro ARegistro() => new() { Desde = Desde, Hasta = Hasta, Origen = Origen, Referencia = Referencia };
}

public interface IReservaRepository
{
    /// <summary>
    /// Crea la reserva si nada ocupa sus noches (reservas vivas, bloqueos manuales, Airbnb). Serializa por alojamiento
    /// con un advisory lock y antes expira las retenciones vencidas. Devuelve null si las fechas ya no están libres.
    /// </summary>
    Task<long?> CrearSiDisponibleAsync(ReservaNueva reserva, CancellationToken ct);

    Task<ReservaRegistro?> ObtenerAsync(long id, CancellationToken ct);
    Task<ReservaRegistro?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct);
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct);

    Task<(IReadOnlyList<ReservaRegistro> Items, int Total)> ListarAsync(
        string? estado, string? busqueda, PaginationParams p, CancellationToken ct);

    /// <summary>Suma un intento de pago con Wompi y devuelve la referencia nueva (<c>CODIGO-N</c>).</summary>
    Task<string> NuevoIntentoPagoAsync(long id, CancellationToken ct);

    /// <summary>Guarda el último estado conocido del pago sin cambiar el estado de la reserva.</summary>
    Task RegistrarPagoAsync(long id, TransaccionPago tx, CancellationToken ct);

    /// <summary>Confirma si las noches siguen libres (ignorando la propia reserva). Serializa por alojamiento.</summary>
    Task<ResultadoConfirmacion> ConfirmarSiDisponibleAsync(long id, string proveedor, TransaccionPago? tx, CancellationToken ct);

    Task<bool> CambiarEstadoAsync(long id, string estado, string? motivo, CancellationToken ct);
    Task<bool> ActualizarNotasAsync(long id, string? notas, CancellationToken ct);
    Task AgregarNotaAsync(long id, string nota, CancellationToken ct);

    /// <summary>Pasa a <c>expirada</c> las retenciones vencidas. Devuelve cuántas.</summary>
    Task<int> ExpirarVencidasAsync(CancellationToken ct);

    /// <summary>Pendientes con una transacción de Wompi sin resultado final: el worker las reconsulta.</summary>
    Task<IReadOnlyList<ReservaRegistro>> PendientesConTransaccionAsync(CancellationToken ct);
}

public enum ResultadoConfirmacion
{
    Confirmada,
    YaEstabaConfirmada,
    FechasOcupadas,
    NoEncontrada
}

public interface IWompiClient
{
    /// <summary>Consulta una transacción (<c>GET /v1/transactions/{id}</c>). null si no existe.</summary>
    Task<TransaccionPago?> ObtenerTransaccionAsync(string id, CancellationToken ct);
}
