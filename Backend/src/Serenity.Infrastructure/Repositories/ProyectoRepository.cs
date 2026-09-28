using Dapper;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class ProyectoRepository(DbConnectionFactory db) : IProyectoRepository
{
    private const string Columnas = """
        id, slug, nombre, etiqueta, icono, ubicacion_etiqueta, titulo, descripcion,
        meta_lugar, meta_estancia, url_reserva, cta_texto, orden, publicado
        """;

    public async Task<IReadOnlyList<ProyectoRegistro>> ListarAsync(bool soloPublicados, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<ProyectoRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM proyectos WHERE (@soloPublicados = FALSE OR publicado) ORDER BY orden, id",
            new { soloPublicados }, cancellationToken: ct))).ToList();
    }

    public async Task<ProyectoRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<ProyectoRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM proyectos WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ProyectoImagenRegistro>> ImagenesAsync(IReadOnlyCollection<long> proyectoIds, CancellationToken ct)
    {
        if (proyectoIds.Count == 0)
        {
            return [];
        }

        using var conn = await db.OpenAsync(ct);
        return (await conn.QueryAsync<ProyectoImagenRegistro>(new CommandDefinition(
            """
            SELECT pi.id, pi.proyecto_id, pi.media_id, m.storage_key, pi.texto_alt, pi.orden
              FROM proyecto_imagenes pi
              JOIN media m ON m.id = pi.media_id
             WHERE pi.proyecto_id = ANY(@ids)
             ORDER BY pi.proyecto_id, pi.orden, pi.id
            """, new { ids = proyectoIds.ToArray() }, cancellationToken: ct))).ToList();
    }

    public async Task<bool> SlugExisteAsync(string slug, long? excluirId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM proyectos WHERE slug = @slug AND (@excluirId::bigint IS NULL OR id <> @excluirId))",
            new { slug, excluirId }, cancellationToken: ct));
    }

    public async Task<long> CrearAsync(ProyectoDatos d, string slug, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO proyectos (slug, nombre, etiqueta, icono, ubicacion_etiqueta, titulo, descripcion,
                                   meta_lugar, meta_estancia, url_reserva, cta_texto, orden, publicado)
            VALUES (@slug, @Nombre, @Etiqueta, @Icono, @UbicacionEtiqueta, @Titulo, @Descripcion,
                    @MetaLugar, @MetaEstancia, @UrlReserva, @CtaTexto,
                    (SELECT COALESCE(MAX(orden), 0) + 1 FROM proyectos), @Publicado)
            RETURNING id
            """,
            new
            {
                slug, d.Nombre, d.Etiqueta, d.Icono, d.UbicacionEtiqueta, d.Titulo, d.Descripcion,
                d.MetaLugar, d.MetaEstancia, d.UrlReserva, d.CtaTexto, d.Publicado
            }, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAsync(long id, ProyectoDatos d, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE proyectos
               SET nombre = @Nombre, etiqueta = @Etiqueta, icono = @Icono, ubicacion_etiqueta = @UbicacionEtiqueta,
                   titulo = @Titulo, descripcion = @Descripcion, meta_lugar = @MetaLugar, meta_estancia = @MetaEstancia,
                   url_reserva = @UrlReserva, cta_texto = @CtaTexto, publicado = @Publicado, actualizado_en = now()
             WHERE id = @id
            """,
            new
            {
                id, d.Nombre, d.Etiqueta, d.Icono, d.UbicacionEtiqueta, d.Titulo, d.Descripcion,
                d.MetaLugar, d.MetaEstancia, d.UrlReserva, d.CtaTexto, d.Publicado
            }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM proyectos WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }

    public async Task ReordenarAsync(IReadOnlyList<long> ids, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE proyectos p SET orden = x.ord, actualizado_en = now()
              FROM unnest(@ids::bigint[]) WITH ORDINALITY AS x(id, ord)
             WHERE p.id = x.id
            """, new { ids = ids.ToArray() }, cancellationToken: ct));
    }

    public async Task<long> AgregarImagenAsync(long proyectoId, long mediaId, string textoAlt, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO proyecto_imagenes (proyecto_id, media_id, texto_alt, orden)
            VALUES (@proyectoId, @mediaId, @textoAlt,
                    (SELECT COALESCE(MAX(orden), 0) + 1 FROM proyecto_imagenes WHERE proyecto_id = @proyectoId))
            RETURNING id
            """, new { proyectoId, mediaId, textoAlt }, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAltImagenAsync(long proyectoId, long imagenId, string textoAlt, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE proyecto_imagenes SET texto_alt = @textoAlt WHERE id = @imagenId AND proyecto_id = @proyectoId",
            new { proyectoId, imagenId, textoAlt }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarImagenAsync(long proyectoId, long imagenId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM proyecto_imagenes WHERE id = @imagenId AND proyecto_id = @proyectoId",
            new { proyectoId, imagenId }, cancellationToken: ct)) > 0;
    }

    public async Task ReordenarImagenesAsync(long proyectoId, IReadOnlyList<long> ids, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE proyecto_imagenes pi SET orden = x.ord
              FROM unnest(@ids::bigint[]) WITH ORDINALITY AS x(id, ord)
             WHERE pi.id = x.id AND pi.proyecto_id = @proyectoId
            """, new { proyectoId, ids = ids.ToArray() }, cancellationToken: ct));
    }
}
