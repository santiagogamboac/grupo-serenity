using Dapper;
using Serenity.Application.Interfaces;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Infrastructure.Repositories;

internal sealed class EstadoAppRepository(DbConnectionFactory db) : IEstadoAppRepository
{
    public async Task<bool> ExisteAsync(string clave, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM app_state WHERE clave = @clave)", new { clave }, cancellationToken: ct));
    }

    public async Task MarcarAsync(string clave, string valor, CancellationToken ct)
    {
        using var conn = await db.OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "INSERT INTO app_state (clave, valor) VALUES (@clave, @valor) ON CONFLICT (clave) DO UPDATE SET valor = EXCLUDED.valor",
            new { clave, valor }, cancellationToken: ct));
    }
}
