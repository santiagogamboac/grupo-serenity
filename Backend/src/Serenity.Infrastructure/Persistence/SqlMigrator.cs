using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Serenity.Infrastructure.Persistence;

/// <summary>
/// Aplica las migraciones SQL embebidas (<c>Migrations/V###__nombre.sql</c>) en orden y
/// registra cada una en <c>schema_migrations</c>. Cada migración corre en su propia
/// transacción, y un <c>pg_advisory_lock</c> evita que dos instancias arrancando a la vez
/// apliquen lo mismo (ver Task/00-plan-general.md, D2).
/// </summary>
public sealed class SqlMigrator(DbConnectionFactory conexiones, ILogger<SqlMigrator> logger)
{
    // Clave arbitraria pero fija del lock consultivo.
    private const long ClaveLock = 7_311_902_845_001;

    /// <param name="trasMigrar">
    /// Se ejecuta con el lock aún tomado (p. ej. el seed), de modo que dos réplicas no siembren a la vez.
    /// </param>
    public async Task MigrarAsync(Func<CancellationToken, Task>? trasMigrar = null, CancellationToken ct = default)
    {
        await using var conn = (NpgsqlConnection)await AbrirConReintentosAsync(ct);

        await conn.ExecuteAsync("SELECT pg_advisory_lock(@k)", new { k = ClaveLock });
        try
        {
            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    version     VARCHAR(200) PRIMARY KEY,
                    aplicada_en TIMESTAMPTZ  NOT NULL DEFAULT now()
                )
                """);

            var aplicadas = (await conn.QueryAsync<string>("SELECT version FROM schema_migrations")).ToHashSet();

            foreach (var (nombre, sql) in LeerMigraciones())
            {
                if (aplicadas.Contains(nombre))
                {
                    continue;
                }

                logger.LogInformation("Aplicando migración {Migracion}...", nombre);

                await using var tx = await conn.BeginTransactionAsync(ct);
                await conn.ExecuteAsync(sql, transaction: tx);
                await conn.ExecuteAsync(
                    "INSERT INTO schema_migrations (version) VALUES (@nombre)", new { nombre }, tx);
                await tx.CommitAsync(ct);
            }

            if (trasMigrar is not null)
            {
                await trasMigrar(ct);
            }
        }
        finally
        {
            await conn.ExecuteAsync("SELECT pg_advisory_unlock(@k)", new { k = ClaveLock });
        }
    }

    private async Task<System.Data.IDbConnection> AbrirConReintentosAsync(CancellationToken ct)
    {
        // En docker compose la API puede arrancar antes de que Postgres acepte conexiones.
        const int intentos = 30;
        for (var i = 1; ; i++)
        {
            try
            {
                return await conexiones.OpenAsync(ct);
            }
            catch (Exception ex) when (i < intentos && ex is NpgsqlException or System.Net.Sockets.SocketException)
            {
                logger.LogWarning("Postgres aún no disponible (intento {Intento}/{Total}): {Mensaje}", i, intentos, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private static IEnumerable<(string Nombre, string Sql)> LeerMigraciones()
    {
        var assembly = typeof(SqlMigrator).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.Ordinal))
            .Select(n => (Recurso: n, Nombre: n[(n.IndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..]))
            .OrderBy(x => x.Nombre, StringComparer.Ordinal)
            .Select(x =>
            {
                using var stream = assembly.GetManifestResourceStream(x.Recurso)!;
                using var reader = new StreamReader(stream);
                return (x.Nombre, reader.ReadToEnd());
            });
    }
}
