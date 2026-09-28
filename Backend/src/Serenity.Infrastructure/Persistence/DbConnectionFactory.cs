using System.Data;
using Npgsql;

namespace Serenity.Infrastructure.Persistence;

/// <summary>Factory de conexiones Npgsql para Dapper. Singleton: la cadena no cambia.</summary>
public sealed class DbConnectionFactory
{
    private readonly NpgsqlDataSource _dataSource;

    public DbConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        // El DataSource agrupa las conexiones (pool) y se comparte en toda la app.
        _dataSource = NpgsqlDataSource.Create(connectionString);
    }

    /// <summary>Abre una conexión del pool. El llamador la cierra (using).</summary>
    public async Task<IDbConnection> OpenAsync(CancellationToken ct = default)
        => await _dataSource.OpenConnectionAsync(ct);
}
