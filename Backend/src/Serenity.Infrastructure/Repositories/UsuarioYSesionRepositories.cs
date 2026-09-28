using Dapper;
using Serenity.Application.Interfaces;
using Serenity.Domain.Entities;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class UsuarioRepository(DbConnectionFactory db) : IUsuarioRepository
{
    private const string Columnas = "id, nombre, correo, password_hash, rol, activo, intentos_fallidos, bloqueado_hasta";

    public async Task<Usuario?> ObtenerPorCorreoAsync(string correo, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            new CommandDefinition($"SELECT {Columnas} FROM usuarios WHERE lower(correo) = lower(@correo)", new { correo }, cancellationToken: ct));
    }

    public async Task<Usuario?> ObtenerPorIdAsync(long id, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Usuario>(
            new CommandDefinition($"SELECT {Columnas} FROM usuarios WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<long> CrearAsync(Usuario u, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO usuarios (nombre, correo, password_hash, rol, activo)
            VALUES (@Nombre, @Correo, @PasswordHash, @Rol, @Activo)
            RETURNING id
            """, u, cancellationToken: ct));
    }

    public async Task<int> ContarAsync(CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*)::int FROM usuarios", cancellationToken: ct));
    }

    public async Task GuardarBloqueoAsync(Usuario u, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE usuarios
               SET intentos_fallidos = @IntentosFallidos, bloqueado_hasta = @BloqueadoHasta, actualizado_en = now()
             WHERE id = @Id
            """, u, cancellationToken: ct));
    }

    public async Task ActualizarPasswordAsync(long usuarioId, string passwordHash, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE usuarios SET password_hash = @passwordHash, actualizado_en = now() WHERE id = @usuarioId",
            new { usuarioId, passwordHash }, cancellationToken: ct));
    }
}

internal sealed class RefreshTokenRepository(DbConnectionFactory db) : IRefreshTokenRepository
{
    public async Task GuardarAsync(long usuarioId, string tokenHash, DateTime expiraEnUtc, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO refresh_tokens (usuario_id, token_hash, expira_en) VALUES (@usuarioId, @tokenHash, @expiraEnUtc)",
            new { usuarioId, tokenHash, expiraEnUtc }, cancellationToken: ct));
    }

    public async Task<long?> ConsumirAsync(string tokenHash, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        // Atómico: dos refresh simultáneos con el mismo token no pueden ganar ambos.
        return await conn.ExecuteScalarAsync<long?>(new CommandDefinition(
            """
            UPDATE refresh_tokens
               SET revocado_en = now()
             WHERE token_hash = @tokenHash AND revocado_en IS NULL AND expira_en > now()
            RETURNING usuario_id
            """, new { tokenHash }, cancellationToken: ct));
    }

    public async Task RevocarTodosAsync(long usuarioId, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE refresh_tokens SET revocado_en = now() WHERE usuario_id = @usuarioId AND revocado_en IS NULL",
            new { usuarioId }, cancellationToken: ct));
    }
}
