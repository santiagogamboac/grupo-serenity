using System.Data;
using Dapper;
using Npgsql;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class ReservaRepository(DbConnectionFactory db) : IReservaRepository
{
    /// <summary>Primera clave del advisory lock por alojamiento (la segunda es el id del alojamiento).</summary>
    private const int ClaveLock = 7_317;

    private const string Select = """
        SELECT r.id, r.codigo, r.token_consulta, r.alojamiento_id, a.nombre AS alojamiento_nombre, a.proyecto_id,
               p.nombre AS proyecto_nombre, r.llegada, r.salida, r.huespedes, r.nombre, r.correo, r.telefono, r.nota_huesped,
               r.noches, r.subtotal, r.tarifa_aseo, r.total, r.moneda, r.estado, r.motivo_estado, r.expira_en,
               r.pago_proveedor, r.pago_intentos, r.pago_referencia, r.pago_transaccion_id, r.pago_estado, r.pago_metodo,
               r.pagado_en, r.notas, r.creado_en, r.actualizado_en
          FROM reservas r
          JOIN alojamientos a ON a.id = r.alojamiento_id
          JOIN proyectos p ON p.id = a.proyecto_id
        """;

    private static readonly string Vencida =
        $"estado = 'pendiente_pago' AND NOT {AlojamientoRepository.ReservaVigente("reservas")}";

    public async Task<long?> CrearSiDisponibleAsync(ReservaNueva r, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        await BloquearAlojamientoAsync(conn, tx, r.AlojamientoId, ct);

        var ocupado = await AlojamientoRepository.OcupacionAsync(conn, tx, [r.AlojamientoId], r.Llegada, r.Salida, null, ct);
        if (ocupado.Count > 0)
        {
            return null;
        }

        try
        {
            var id = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO reservas (codigo, token_consulta, alojamiento_id, llegada, salida, huespedes, nombre, correo, telefono,
                                      nota_huesped, noches, subtotal, tarifa_aseo, total, expira_en)
                VALUES (@Codigo, @TokenConsulta, @AlojamientoId, @Llegada, @Salida, @Huespedes, @Nombre, @Correo, @Telefono,
                        @NotaHuesped, @Noches, @Subtotal, @TarifaAseo, @Total, @ExpiraEn)
                RETURNING id
                """, r, tx, cancellationToken: ct));

            tx.Commit();
            return id;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return null;
        }
    }

    public async Task<ReservaRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ReservaRegistro>(
            new CommandDefinition($"{Select} WHERE r.id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<ReservaRegistro?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ReservaRegistro>(
            new CommandDefinition($"{Select} WHERE r.codigo = @codigo", new { codigo }, cancellationToken: ct));
    }

    public async Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM reservas WHERE codigo = @codigo)", new { codigo }, cancellationToken: ct));
    }

    public async Task<(IReadOnlyList<ReservaRegistro> Items, int Total)> ListarAsync(
        string? estado, string? busqueda, PaginationParams p, CancellationToken ct)
    {
        const string filtro = """
            WHERE (@estado::text IS NULL OR r.estado = @estado)
              AND (@patron::text IS NULL OR r.codigo ILIKE @patron OR r.nombre ILIKE @patron OR r.correo ILIKE @patron
                   OR r.telefono ILIKE @patron OR a.nombre ILIKE @patron OR p.nombre ILIKE @patron)
            """;

        // Los comodines de LIKE que escriba el usuario se escapan para que la búsqueda sea literal.
        var patron = busqueda is null
            ? null
            : "%" + busqueda.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        using var conn = await db.OpenAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT COUNT(*)::int FROM reservas r
              JOIN alojamientos a ON a.id = r.alojamiento_id JOIN proyectos p ON p.id = a.proyecto_id
            {filtro}
            """, new { estado, patron }, cancellationToken: ct));

        var items = (await conn.QueryAsync<ReservaRegistro>(new CommandDefinition(
            $"{Select} {filtro} ORDER BY r.creado_en DESC, r.id DESC LIMIT @limit OFFSET @offset",
            new { estado, patron, limit = p.PageSize, offset = p.Offset }, cancellationToken: ct))).ToList();

        return (items, total);
    }

    public async Task<string> NuevoIntentoPagoAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<string>(new CommandDefinition(
            """
            UPDATE reservas
               SET pago_intentos = pago_intentos + 1, pago_proveedor = 'wompi',
                   pago_referencia = codigo || '-' || (pago_intentos + 1), actualizado_en = now()
             WHERE id = @id
            RETURNING pago_referencia
            """, new { id }, cancellationToken: ct)) ?? throw new InvalidOperationException($"La reserva {id} no existe.");
    }

    public async Task RegistrarPagoAsync(long id, TransaccionPago t, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE reservas
               SET pago_proveedor = 'wompi', pago_transaccion_id = @Id, pago_estado = @Estado,
                   pago_metodo = COALESCE(@Metodo, pago_metodo), actualizado_en = now()
             WHERE id = @reservaId
            """, new { reservaId = id, t.Id, t.Estado, t.Metodo }, cancellationToken: ct));
    }

    public async Task<ResultadoConfirmacion> ConfirmarSiDisponibleAsync(long id, string proveedor, TransaccionPago? t, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        var alojamientoId = await conn.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT alojamiento_id FROM reservas WHERE id = @id", new { id }, cancellationToken: ct));
        if (alojamientoId is null)
        {
            return ResultadoConfirmacion.NoEncontrada;
        }

        using var tx = conn.BeginTransaction();
        await BloquearAlojamientoAsync(conn, tx, alojamientoId.Value, ct);

        var r = await conn.QuerySingleAsync<EstadoFechas>(new CommandDefinition(
            "SELECT estado, llegada, salida FROM reservas WHERE id = @id FOR UPDATE", new { id }, tx, cancellationToken: ct));
        if (r.Estado == "confirmada")
        {
            return ResultadoConfirmacion.YaEstabaConfirmada;
        }

        var ocupado = await AlojamientoRepository.OcupacionAsync(conn, tx, [alojamientoId.Value], r.Llegada, r.Salida, id, ct);
        // Un bloqueo de Airbnb con exactamente estas fechas es casi seguro el eco de esta misma reserva
        // (nuestro .ics importado en Airbnb y vuelto a leer de su calendario): no cuenta como conflicto.
        if (ocupado.Any(o => !(o.Origen == "airbnb" && o.Desde == r.Llegada && o.Hasta == r.Salida)))
        {
            return ResultadoConfirmacion.FechasOcupadas;
        }

        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE reservas
                   SET estado = 'confirmada', motivo_estado = NULL, expira_en = NULL, pago_proveedor = @proveedor,
                       pago_transaccion_id = COALESCE(@TxId, pago_transaccion_id), pago_estado = COALESCE(@TxEstado, pago_estado),
                       pago_metodo = COALESCE(@TxMetodo, pago_metodo), pagado_en = COALESCE(pagado_en, now()), actualizado_en = now()
                 WHERE id = @id
                """, new { id, proveedor, TxId = t?.Id, TxEstado = t?.Estado, TxMetodo = t?.Metodo }, tx, cancellationToken: ct));

            tx.Commit();
            return ResultadoConfirmacion.Confirmada;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return ResultadoConfirmacion.FechasOcupadas;
        }
    }

    public async Task<bool> CambiarEstadoAsync(long id, string estado, string? motivo, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE reservas
               SET estado = @estado, motivo_estado = @motivo,
                   expira_en = CASE WHEN @estado = 'pendiente_pago' THEN expira_en ELSE NULL END, actualizado_en = now()
             WHERE id = @id
            """, new { id, estado, motivo }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> ActualizarNotasAsync(long id, string? notas, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE reservas SET notas = @notas, actualizado_en = now() WHERE id = @id", new { id, notas }, cancellationToken: ct)) > 0;
    }

    public async Task AgregarNotaAsync(long id, string nota, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE reservas
               SET notas = CASE WHEN COALESCE(notas, '') = '' THEN @nota ELSE notas || E'\n' || @nota END, actualizado_en = now()
             WHERE id = @id
            """, new { id, nota = $"[{FormatoColombia.FechaHora(DateTime.UtcNow)}] {nota}" }, cancellationToken: ct));
    }

    public async Task<int> ExpirarVencidasAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE reservas
               SET estado = 'expirada', motivo_estado = 'No se recibió el pago a tiempo.', expira_en = NULL, actualizado_en = now()
             WHERE {Vencida}
            """, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ReservaRegistro>> PendientesConTransaccionAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<ReservaRegistro>(new CommandDefinition(
            $"{Select} WHERE r.estado = 'pendiente_pago' AND r.pago_transaccion_id IS NOT NULL AND r.pago_estado = 'PENDING' ORDER BY r.id",
            cancellationToken: ct))).ToList();
    }

    private sealed class EstadoFechas
    {
        public string Estado { get; init; } = default!;
        public DateOnly Llegada { get; init; }
        public DateOnly Salida { get; init; }
    }

    /// <summary>
    /// Serializa las operaciones sobre el calendario de un alojamiento hasta el fin de la transacción, y antes de
    /// comprobar huecos pasa a expiradas sus retenciones vencidas (la restricción de exclusión aún las vería vivas).
    /// </summary>
    private static async Task BloquearAlojamientoAsync(IDbConnection conn, IDbTransaction tx, long alojamientoId, CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(@ClaveLock, @alojamientoId::int)", new { ClaveLock, alojamientoId }, tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition(
            $"""
            UPDATE reservas
               SET estado = 'expirada', motivo_estado = 'No se recibió el pago a tiempo.', expira_en = NULL, actualizado_en = now()
             WHERE alojamiento_id = @alojamientoId AND {Vencida}
            """, new { alojamientoId }, tx, cancellationToken: ct));
    }
}
