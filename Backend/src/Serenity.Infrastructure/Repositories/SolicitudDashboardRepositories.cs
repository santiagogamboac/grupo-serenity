using Dapper;
using Serenity.Application.Common;
using Serenity.Application.Interfaces;
using Serenity.Application.Models;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class SolicitudRepository(DbConnectionFactory db) : ISolicitudRepository
{
    private const string Columnas = """
        id, nombre, contacto, fecha_llegada, huespedes, proyecto_id, destino_texto, estado, notas, creado_en, actualizado_en
        """;

    public async Task<long> CrearAsync(SolicitudNueva s, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO solicitudes (nombre, contacto, fecha_llegada, huespedes, proyecto_id, destino_texto)
            VALUES (@Nombre, @Contacto, @FechaLlegada, @Huespedes, @ProyectoId, @DestinoTexto)
            RETURNING id
            """, s, cancellationToken: ct));
    }

    public async Task<(IReadOnlyList<SolicitudRegistro> Items, int Total)> ListarAsync(
        string? estado, string? busqueda, PaginationParams p, CancellationToken ct)
    {
        const string filtro = """
            WHERE (@estado::text IS NULL OR estado = @estado)
              AND (@patron::text IS NULL OR nombre ILIKE @patron OR contacto ILIKE @patron OR destino_texto ILIKE @patron)
            """;

        // Los comodines de LIKE que escriba el usuario se escapan para que la búsqueda sea literal.
        var patron = busqueda is null
            ? null
            : "%" + busqueda.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        using var conn = await db.OpenAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*)::int FROM solicitudes {filtro}", new { estado, patron }, cancellationToken: ct));

        var items = (await conn.QueryAsync<SolicitudRegistro>(new CommandDefinition(
            $"SELECT {Columnas} FROM solicitudes {filtro} ORDER BY creado_en DESC, id DESC LIMIT @limit OFFSET @offset",
            new { estado, patron, limit = p.PageSize, offset = p.Offset }, cancellationToken: ct))).ToList();

        return (items, total);
    }

    public async Task<SolicitudRegistro?> ObtenerAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SolicitudRegistro>(
            new CommandDefinition($"SELECT {Columnas} FROM solicitudes WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<bool> ActualizarAsync(long id, string estado, string? notas, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE solicitudes SET estado = @estado, notas = @notas, actualizado_en = now() WHERE id = @id",
            new { id, estado, notas }, cancellationToken: ct)) > 0;
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition("DELETE FROM solicitudes WHERE id = @id", new { id }, cancellationToken: ct)) > 0;
    }
}

internal sealed class DashboardRepository(DbConnectionFactory db) : IDashboardRepository
{
    public async Task<DashboardDto> ObtenerAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleAsync<DashboardDto>(new CommandDefinition(
            """
            SELECT
              (SELECT COUNT(*) FROM solicitudes WHERE estado = 'nueva')::int AS solicitudes_nuevas,
              (SELECT COUNT(*) FROM solicitudes)::int                        AS solicitudes_total,
              (SELECT COUNT(*) FROM proyectos WHERE publicado)::int          AS proyectos_publicados,
              (SELECT COUNT(*) FROM proyectos)::int                          AS proyectos_total,
              (SELECT COUNT(*) FROM resenas WHERE publicada)::int            AS resenas_publicadas,
              (SELECT COUNT(*) FROM valores WHERE activo)::int               AS valores_activos
            """, cancellationToken: ct));
    }
}
