using Dapper;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class MediaRepository(DbConnectionFactory db) : IMediaRepository
{
    private const string Columnas = "id, storage_key, nombre_original, content_type, tamano_bytes, creado_en";

    public async Task<long> CrearAsync(MediaRegistro m, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO media (storage_key, nombre_original, content_type, tamano_bytes, creado_en)
            VALUES (@StorageKey, @NombreOriginal, @ContentType, @TamanoBytes, @CreadoEn)
            RETURNING id
            """, m, cancellationToken: ct));
    }

    public async Task<MediaRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<MediaRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM media WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<MediaRegistro>> ObtenerPorIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<MediaRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM media WHERE id = ANY(@ids)", new { ids = ids.ToArray() }, cancellationToken: ct))).ToList();
    }

    public async Task<(IReadOnlyList<MediaRegistro> Items, int Total)> ListarAsync(PaginationParams p, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*)::int FROM media", cancellationToken: ct));
        var items = (await conn.QueryAsync<MediaRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM media ORDER BY creado_en DESC, id DESC LIMIT @limit OFFSET @offset",
            new { limit = p.PageSize, offset = p.Offset }, cancellationToken: ct))).ToList();
        return (items, total);
    }

    public async Task<bool> EstaEnUsoAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        // Referencias: galerías de proyectos y cualquier campo "*MediaId" de primer nivel en las secciones.
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM proyecto_imagenes WHERE media_id = @id)
                OR EXISTS (
                    SELECT 1
                      FROM secciones s, jsonb_each(s.contenido) e
                     WHERE e.key ILIKE '%mediaid' AND e.value = to_jsonb(@id::bigint)
                )
            """, new { id }, cancellationToken: ct));
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM media WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }
}

internal sealed class SeccionRepository(DbConnectionFactory db) : ISeccionRepository
{
    private sealed class Fila
    {
        public string Clave { get; init; } = default!;
        public string Contenido { get; init; } = default!;
    }

    public async Task<IReadOnlyDictionary<string, string>> ObtenerTodasAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        var filas = await conn.QueryAsync<Fila>(new CommandDefinition(
            "SELECT clave, contenido::text AS contenido FROM secciones", cancellationToken: ct));
        return filas.ToDictionary(f => f.Clave, f => f.Contenido);
    }

    public async Task GuardarAsync(string clave, string json, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO secciones (clave, contenido, actualizado_en) VALUES (@clave, @json::jsonb, now())
            ON CONFLICT (clave) DO UPDATE SET contenido = EXCLUDED.contenido, actualizado_en = now()
            """, new { clave, json }, cancellationToken: ct));
    }
}
