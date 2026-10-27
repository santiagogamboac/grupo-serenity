using System.Data;
using Dapper;
using Npgsql;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class AlojamientoRepository(DbConnectionFactory db) : IAlojamientoRepository
{
    private const string Columnas = """
        id, proyecto_id, nombre, descripcion, imagen_media_id, capacidad, huespedes_incluidos, precio_noche,
        recargo_huesped_extra, tarifa_aseo, estancia_minima, anuncio_id, token_ical, orden, publicado
        """;

    public async Task<IReadOnlyList<AlojamientoRegistro>> ListarPorProyectoAsync(long proyectoId, bool soloPublicados, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<AlojamientoRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM alojamientos WHERE proyecto_id = @proyectoId AND (@soloPublicados = FALSE OR publicado) ORDER BY orden, id",
            new { proyectoId, soloPublicados }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<AlojamientoRegistro>> ListarPublicosAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<AlojamientoRegistro>(new CommandDefinition(
            $"""
            SELECT {string.Join(", ", Columnas.Split(',').Select(c => "a." + c.Trim()))}
              FROM alojamientos a JOIN proyectos p ON p.id = a.proyecto_id
             WHERE a.publicado AND p.publicado
             ORDER BY p.orden, p.id, a.orden, a.id
            """, cancellationToken: ct))).ToList();
    }

    public async Task<AlojamientoRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AlojamientoRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM alojamientos WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<AlojamientoRegistro?> ObtenerPorTokenIcalAsync(string token, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AlojamientoRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM alojamientos WHERE token_ical = @token", new { token }, cancellationToken: ct));
    }

    public async Task<long> CrearAsync(long proyectoId, AlojamientoDatos d, string tokenIcal, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO alojamientos (proyecto_id, nombre, descripcion, imagen_media_id, capacidad, huespedes_incluidos, precio_noche,
                                      recargo_huesped_extra, tarifa_aseo, estancia_minima, anuncio_id, token_ical, publicado, orden)
            VALUES (@proyectoId, @Nombre, @Descripcion, @ImagenMediaId, @Capacidad, @HuespedesIncluidos, @PrecioNoche,
                    @RecargoHuespedExtra, @TarifaAseo, @EstanciaMinima, @AnuncioId, @tokenIcal, @Publicado,
                    (SELECT COALESCE(MAX(orden), 0) + 1 FROM alojamientos WHERE proyecto_id = @proyectoId))
            RETURNING id
            """,
            new
            {
                proyectoId, d.Nombre, d.Descripcion, d.ImagenMediaId, d.Capacidad, d.HuespedesIncluidos, d.PrecioNoche,
                d.RecargoHuespedExtra, d.TarifaAseo, d.EstanciaMinima, d.AnuncioId, tokenIcal, d.Publicado
            }, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAsync(long id, AlojamientoDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE alojamientos
               SET nombre = @Nombre, descripcion = @Descripcion, imagen_media_id = @ImagenMediaId, capacidad = @Capacidad,
                   huespedes_incluidos = @HuespedesIncluidos, precio_noche = @PrecioNoche, recargo_huesped_extra = @RecargoHuespedExtra,
                   tarifa_aseo = @TarifaAseo, estancia_minima = @EstanciaMinima, anuncio_id = @AnuncioId, publicado = @Publicado,
                   actualizado_en = now()
             WHERE id = @id
            """,
            new
            {
                id, d.Nombre, d.Descripcion, d.ImagenMediaId, d.Capacidad, d.HuespedesIncluidos, d.PrecioNoche,
                d.RecargoHuespedExtra, d.TarifaAseo, d.EstanciaMinima, d.AnuncioId, d.Publicado
            }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM alojamientos WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> TieneReservasAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM reservas WHERE alojamiento_id = @id)", new { id }, cancellationToken: ct));
    }

    public async Task<bool> ProyectoTieneReservasAsync(long proyectoId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM reservas r JOIN alojamientos a ON a.id = r.alojamiento_id WHERE a.proyecto_id = @proyectoId)",
            new { proyectoId }, cancellationToken: ct));
    }

    public async Task<bool> AnuncioVinculadoAsync(long anuncioId, long? excluirId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM alojamientos WHERE anuncio_id = @anuncioId AND (@excluirId::bigint IS NULL OR id <> @excluirId))",
            new { anuncioId, excluirId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AlojamientoImagenRegistro>> ImagenesAsync(IReadOnlyCollection<long> alojamientoIds, CancellationToken ct)
    {
        if (alojamientoIds.Count == 0)
        {
            return [];
        }

        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<AlojamientoImagenRegistro>(new CommandDefinition(
            """
            SELECT ai.id, ai.alojamiento_id, ai.media_id, m.storage_key, ai.texto_alt, ai.orden
              FROM alojamiento_imagenes ai
              JOIN media m ON m.id = ai.media_id
             WHERE ai.alojamiento_id = ANY(@ids)
             ORDER BY ai.alojamiento_id, ai.orden, ai.id
            """, new { ids = alojamientoIds.ToArray() }, cancellationToken: ct))).ToList();
    }

    public async Task<long> AgregarImagenAsync(long alojamientoId, long mediaId, string textoAlt, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO alojamiento_imagenes (alojamiento_id, media_id, texto_alt, orden)
            VALUES (@alojamientoId, @mediaId, @textoAlt,
                    (SELECT COALESCE(MAX(orden), 0) + 1 FROM alojamiento_imagenes WHERE alojamiento_id = @alojamientoId))
            RETURNING id
            """, new { alojamientoId, mediaId, textoAlt }, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAltImagenAsync(long alojamientoId, long imagenId, string textoAlt, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE alojamiento_imagenes SET texto_alt = @textoAlt WHERE id = @imagenId AND alojamiento_id = @alojamientoId",
            new { alojamientoId, imagenId, textoAlt }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarImagenAsync(long alojamientoId, long imagenId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM alojamiento_imagenes WHERE id = @imagenId AND alojamiento_id = @alojamientoId",
            new { alojamientoId, imagenId }, cancellationToken: ct)) > 0;
    }

    public async Task ReordenarImagenesAsync(long alojamientoId, IReadOnlyList<long> ids, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE alojamiento_imagenes ai SET orden = x.ord
              FROM unnest(@ids::bigint[]) WITH ORDINALITY AS x(id, ord)
             WHERE ai.id = x.id AND ai.alojamiento_id = @alojamientoId
            """, new { alojamientoId, ids = ids.ToArray() }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<TarifaRegistro>> TarifasAsync(IReadOnlyCollection<long> alojamientoIds, CancellationToken ct)
    {
        if (alojamientoIds.Count == 0)
        {
            return [];
        }

        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<TarifaRegistro>(new CommandDefinition(
            "SELECT id, alojamiento_id, nombre, desde, hasta, precio_noche FROM alojamiento_tarifas WHERE alojamiento_id = ANY(@ids) ORDER BY alojamiento_id, desde",
            new { ids = alojamientoIds.ToArray() }, cancellationToken: ct))).ToList();
    }

    public async Task<long?> CrearTarifaAsync(long alojamientoId, string nombre, DateOnly desde, DateOnly hasta, long precioNoche, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        try
        {
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """
                INSERT INTO alojamiento_tarifas (alojamiento_id, nombre, desde, hasta, precio_noche)
                VALUES (@alojamientoId, @nombre, @desde, @hasta, @precioNoche) RETURNING id
                """, new { alojamientoId, nombre, desde, hasta, precioNoche }, cancellationToken: ct));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return null;
        }
    }

    public async Task<bool> EliminarTarifaAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM alojamiento_tarifas WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task<IReadOnlyList<BloqueoManualRegistro>> BloqueosManualesAsync(long alojamientoId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<BloqueoManualRegistro>(new CommandDefinition(
            "SELECT id, alojamiento_id, desde, hasta, motivo, creado_en FROM alojamiento_bloqueos WHERE alojamiento_id = @alojamientoId ORDER BY desde, id",
            new { alojamientoId }, cancellationToken: ct))).ToList();
    }

    public async Task<long> CrearBloqueoAsync(long alojamientoId, DateOnly desde, DateOnly hasta, string motivo, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "INSERT INTO alojamiento_bloqueos (alojamiento_id, desde, hasta, motivo) VALUES (@alojamientoId, @desde, @hasta, @motivo) RETURNING id",
            new { alojamientoId, desde, hasta, motivo }, cancellationToken: ct));
    }

    public async Task<bool> EliminarBloqueoAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM alojamiento_bloqueos WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task<IReadOnlyList<OcupacionAlojamiento>> OcupacionAsync(
        IReadOnlyCollection<long> alojamientoIds, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (alojamientoIds.Count == 0)
        {
            return [];
        }

        using var conn = await db.OpenAsync(ct);
        return await OcupacionAsync(conn, null, alojamientoIds.ToArray(), desde, hasta, excluirReservaId: null, ct);
    }

    /// <summary>
    /// Reservas que ocupan el calendario: confirmadas, y pendientes cuya retención sigue vigente (o con un pago
    /// PENDING de Wompi, p. ej. PSE, que se espera hasta un día más). Nunca evalúa a NULL: se usa también negada
    /// (<c>NOT vigente</c> para expirar) y un NULL ahí dejaría la retención viva para siempre.
    /// </summary>
    internal static string ReservaVigente(string alias) => $"""
        ({alias}.estado = 'confirmada' OR ({alias}.estado = 'pendiente_pago' AND (
            {alias}.expira_en IS NULL OR {alias}.expira_en > now()
            OR ({alias}.pago_estado IS NOT DISTINCT FROM 'PENDING' AND {alias}.expira_en > now() - interval '1 day'))))
        """;

    internal static async Task<IReadOnlyList<OcupacionAlojamiento>> OcupacionAsync(
        IDbConnection conn, IDbTransaction? tx, long[] ids, DateOnly desde, DateOnly hasta, long? excluirReservaId, CancellationToken ct)
        => (await conn.QueryAsync<OcupacionAlojamiento>(new CommandDefinition(
            $"""
            SELECT r.alojamiento_id, r.llegada AS desde, r.salida AS hasta, 'reserva' AS origen, r.codigo AS referencia
              FROM reservas r
             WHERE r.alojamiento_id = ANY(@ids) AND r.salida > @desde AND r.llegada < @hasta
               AND (@excluirReservaId::bigint IS NULL OR r.id <> @excluirReservaId)
               AND {ReservaVigente("r")}
            UNION ALL
            SELECT b.alojamiento_id, b.desde, b.hasta, 'manual', b.id::text
              FROM alojamiento_bloqueos b
             WHERE b.alojamiento_id = ANY(@ids) AND b.hasta > @desde AND b.desde < @hasta
            UNION ALL
            SELECT a.id, d.desde, d.hasta, 'airbnb', d.id::text
              FROM alojamientos a JOIN disponibilidad_bloqueos d ON d.anuncio_id = a.anuncio_id
             WHERE a.id = ANY(@ids) AND d.hasta > @desde AND d.desde < @hasta
            ORDER BY 2
            """, new { ids, desde, hasta, excluirReservaId }, tx, cancellationToken: ct))).ToList();
}
