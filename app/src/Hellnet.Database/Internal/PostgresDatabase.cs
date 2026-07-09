using System.Diagnostics.CodeAnalysis;
using Dapper;
using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hellnet.Database.Internal;

internal sealed class PostgresDatabase : IDatabase, IDatabaseHealthCheck, IAsyncDisposable, IDisposable
{
    private readonly HellnetDatabaseOptions _options;
    private readonly ILogger<PostgresDatabase> _logger;
    private readonly DateTime _startedAt = DateTime.UtcNow;
    private int _openConnections;

    public PostgresDatabase(HellnetDatabaseOptions options, ILogger<PostgresDatabase> logger)
    {
        _options = options;
        _logger = logger;
    }

    private NpgsqlConnection CreateConnection()
    {
        var conn = new NpgsqlConnection(_options.ConnectionString);
        Interlocked.Increment(ref _openConnections);
        return conn;
    }

    [ExcludeFromCodeCoverage]
    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeout, cancellationToken: ct));
    }

    [ExcludeFromCodeCoverage]
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.QueryAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeout, cancellationToken: ct));
    }

    [ExcludeFromCodeCoverage]
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeout, cancellationToken: ct));
    }

    [ExcludeFromCodeCoverage]
    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeout, cancellationToken: ct));
    }

    [ExcludeFromCodeCoverage]
    public async Task TransactionAsync(Func<IDatabase, Task> action, CancellationToken ct = default)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var db = new TransactionalDatabase(conn, _options);
            await action(db);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync(ct);
            await conn.ExecuteScalarAsync("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return false;
        }
    }

    public Task<DatabaseHealth> GetHealthAsync(CancellationToken ct = default)
        => IsHealthyAsync(ct).ContinueWith(t => new DatabaseHealth(
            t.Result, "postgresql", DateTime.UtcNow - _startedAt,
            _openConnections), ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    void IDisposable.Dispose() { }
}

[ExcludeFromCodeCoverage]
internal sealed class TransactionalDatabase : IDatabase
{
    private readonly NpgsqlConnection _conn;
    private readonly HellnetDatabaseOptions _options;

    public TransactionalDatabase(NpgsqlConnection conn, HellnetDatabaseOptions options)
    {
        _conn = conn;
        _options = options;
    }

    [ExcludeFromCodeCoverage]
    public Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: null, commandTimeout: _options.CommandTimeout, cancellationToken: ct));

    [ExcludeFromCodeCoverage]
    public Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction: null, commandTimeout: _options.CommandTimeout, cancellationToken: ct));

    [ExcludeFromCodeCoverage]
    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, parameters, transaction: null, commandTimeout: _options.CommandTimeout, cancellationToken: ct));

    [ExcludeFromCodeCoverage]
    public Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, transaction: null, commandTimeout: _options.CommandTimeout, cancellationToken: ct));

    [ExcludeFromCodeCoverage]
    public Task TransactionAsync(Func<IDatabase, Task> action, CancellationToken ct = default)
        => throw new InvalidOperationException("Nested transactions are not supported");
}
