using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serenity.Infrastructure.Persistence;

namespace Serenity.Api.Middlewares;

/// <summary>Healthy solo si Postgres responde a un SELECT 1.</summary>
public sealed class DbHealthCheck(DbConnectionFactory db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var conn = await db.OpenAsync(ct);
            await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: ct));
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres no responde.", ex);
        }
    }
}
