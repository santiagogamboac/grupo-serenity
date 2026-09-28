using Dapper;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class ResenaRepository(DbConnectionFactory db) : IResenaRepository
{
    private const string Columnas = "id, autor, contexto, texto, calificacion, orden, publicada";

    public async Task<IReadOnlyList<ResenaRegistro>> ListarAsync(bool soloPublicadas, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<ResenaRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM resenas WHERE (@soloPublicadas = FALSE OR publicada) ORDER BY orden, id",
            new { soloPublicadas }, cancellationToken: ct))).ToList();
    }

    public async Task<ResenaRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ResenaRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM resenas WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<long> CrearAsync(ResenaDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO resenas (autor, contexto, texto, calificacion, orden, publicada)
            VALUES (@Autor, @Contexto, @Texto, @Calificacion, (SELECT COALESCE(MAX(orden), 0) + 1 FROM resenas), @Publicada)
            RETURNING id
            """, d, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAsync(long id, ResenaDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE resenas
               SET autor = @Autor, contexto = @Contexto, texto = @Texto, calificacion = @Calificacion,
                   publicada = @Publicada, actualizado_en = now()
             WHERE id = @id
            """, new { id, d.Autor, d.Contexto, d.Texto, d.Calificacion, d.Publicada }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM resenas WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task ReordenarAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE resenas r SET orden = x.ord, actualizado_en = now()
              FROM unnest(@ids::bigint[]) WITH ORDINALITY AS x(id, ord)
             WHERE r.id = x.id
            """, new { ids = ids.ToArray() }, cancellationToken: ct));
    }
}

internal sealed class ValorRepository(DbConnectionFactory db) : IValorRepository
{
    private const string Columnas = "id, titulo, descripcion, icono, variante, orden, activo";

    public async Task<IReadOnlyList<ValorRegistro>> ListarAsync(bool soloActivos, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<ValorRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM valores WHERE (@soloActivos = FALSE OR activo) ORDER BY orden, id",
            new { soloActivos }, cancellationToken: ct))).ToList();
    }

    public async Task<ValorRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ValorRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM valores WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<long> CrearAsync(ValorDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO valores (titulo, descripcion, icono, variante, orden, activo)
            VALUES (@Titulo, @Descripcion, @Icono, @Variante, (SELECT COALESCE(MAX(orden), 0) + 1 FROM valores), @Activo)
            RETURNING id
            """, d, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAsync(long id, ValorDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE valores
               SET titulo = @Titulo, descripcion = @Descripcion, icono = @Icono, variante = @Variante,
                   activo = @Activo, actualizado_en = now()
             WHERE id = @id
            """, new { id, d.Titulo, d.Descripcion, d.Icono, d.Variante, d.Activo }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM valores WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task ReordenarAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE valores v SET orden = x.ord, actualizado_en = now()
              FROM unnest(@ids::bigint[]) WITH ORDINALITY AS x(id, ord)
             WHERE v.id = x.id
            """, new { ids = ids.ToArray() }, cancellationToken: ct));
    }
}
