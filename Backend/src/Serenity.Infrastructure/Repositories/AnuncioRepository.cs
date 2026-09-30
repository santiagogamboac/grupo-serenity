using Dapper;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class AnuncioRepository(DbConnectionFactory db) : IAnuncioRepository
{
    private const string Columnas = """
        id, proyecto_id, nombre, airbnb_room_id, url_anuncio, ical_url, orden, publicado,
        ultima_sync_intento, ultima_sync_ok, ultima_sync_error
        """;

    public async Task<IReadOnlyList<AnuncioRegistro>> ListarPorProyectoAsync(long proyectoId, bool soloPublicados, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<AnuncioRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM anuncios WHERE proyecto_id = @proyectoId AND (@soloPublicados = FALSE OR publicado) ORDER BY orden, id",
            new { proyectoId, soloPublicados }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<AnuncioRegistro>> ListarSincronizablesAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<AnuncioRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM anuncios WHERE publicado AND ical_url IS NOT NULL ORDER BY id",
            cancellationToken: ct))).ToList();
    }

    public async Task<AnuncioRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<AnuncioRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM anuncios WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<bool> RoomIdExisteAsync(string roomId, long? excluirId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM anuncios WHERE airbnb_room_id = @roomId AND (@excluirId::bigint IS NULL OR id <> @excluirId))",
            new { roomId, excluirId }, cancellationToken: ct));
    }

    public async Task<long> CrearAsync(long proyectoId, AnuncioDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO anuncios (proyecto_id, nombre, airbnb_room_id, url_anuncio, ical_url, publicado, orden)
            VALUES (@proyectoId, @Nombre, @AirbnbRoomId, @UrlAnuncio, @IcalUrl, @Publicado,
                    (SELECT COALESCE(MAX(orden), 0) + 1 FROM anuncios WHERE proyecto_id = @proyectoId))
            RETURNING id
            """,
            new { proyectoId, d.Nombre, d.AirbnbRoomId, d.UrlAnuncio, d.IcalUrl, d.Publicado }, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAsync(long id, AnuncioDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        var actual = await conn.QuerySingleOrDefaultAsync<AnuncioRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM anuncios WHERE id = @id FOR UPDATE", new { id }, tx, cancellationToken: ct));
        if (actual is null)
        {
            return false;
        }

        var cambioIcal = actual.IcalUrl != d.IcalUrl;
        if (cambioIcal)
        {
            // Los bloqueos del calendario anterior ya no describen este anuncio.
            await conn.ExecuteAsync(new CommandDefinition(
                "DELETE FROM disponibilidad_bloqueos WHERE anuncio_id = @id AND origen = 'airbnb'", new { id }, tx, cancellationToken: ct));
        }

        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE anuncios
               SET nombre = @Nombre, airbnb_room_id = @AirbnbRoomId, url_anuncio = @UrlAnuncio, ical_url = @IcalUrl,
                   publicado = @Publicado, actualizado_en = now(),
                   ultima_sync_intento = CASE WHEN @cambioIcal THEN NULL ELSE ultima_sync_intento END,
                   ultima_sync_ok      = CASE WHEN @cambioIcal THEN NULL ELSE ultima_sync_ok END,
                   ultima_sync_error   = CASE WHEN @cambioIcal THEN NULL ELSE ultima_sync_error END
             WHERE id = @id
            """,
            new { id, d.Nombre, d.AirbnbRoomId, d.UrlAnuncio, d.IcalUrl, d.Publicado, cambioIcal }, tx, cancellationToken: ct));

        tx.Commit();
        return true;
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM anuncios WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task GuardarSincronizacionAsync(long id, IReadOnlyList<BloqueoIcal> bloqueos, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM disponibilidad_bloqueos WHERE anuncio_id = @id AND origen = 'airbnb'", new { id }, tx, cancellationToken: ct));

        if (bloqueos.Count > 0)
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO disponibilidad_bloqueos (anuncio_id, desde, hasta, origen, uid_externo) VALUES (@id, @Desde, @Hasta, 'airbnb', @Uid)",
                bloqueos.Select(b => new { id, b.Desde, b.Hasta, Uid = b.Uid is { Length: > 200 } u ? u[..200] : b.Uid }),
                tx, cancellationToken: ct));
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE anuncios SET ultima_sync_intento = now(), ultima_sync_ok = now(), ultima_sync_error = NULL WHERE id = @id",
            new { id }, tx, cancellationToken: ct));

        tx.Commit();
    }

    public async Task GuardarErrorSincronizacionAsync(long id, string error, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE anuncios SET ultima_sync_intento = now(), ultima_sync_error = @error WHERE id = @id",
            new { id, error }, cancellationToken: ct));
    }

    public async Task<bool> TomarTurnoSincronizacionAsync(long id, int minSegundos, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE anuncios SET ultima_sync_intento = now()
             WHERE id = @id
               AND (ultima_sync_intento IS NULL OR ultima_sync_intento < now() - make_interval(secs => @minSegundos))
            """,
            new { id, minSegundos }, cancellationToken: ct)) > 0;
    }

    public async Task<IReadOnlyList<BloqueoRegistro>> BloqueosAsync(
        IReadOnlyCollection<long> anuncioIds, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (anuncioIds.Count == 0)
        {
            return [];
        }

        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<BloqueoRegistro>(new CommandDefinition(
            """
            SELECT anuncio_id, desde, hasta
              FROM disponibilidad_bloqueos
             WHERE anuncio_id = ANY(@ids) AND hasta > @desde AND desde < @hasta
             ORDER BY anuncio_id, desde
            """, new { ids = anuncioIds.ToArray(), desde, hasta }, cancellationToken: ct))).ToList();
    }
}
