using System.Diagnostics.CodeAnalysis;
using Dapper;
using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hellnet.Database.PostgreSql;

internal sealed class NpgsqlTransaction : IDatabaseTransaction
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly HellnetDatabaseOptions _options;
    private readonly ILogger<NpgsqlTransaction> _logger;

    public NpgsqlTransaction(
        NpgsqlDataSource dataSource,
        HellnetDatabaseOptions options,
        ILogger<NpgsqlTransaction> logger)
    {
        _dataSource = dataSource;
        _options = options;
        _logger = logger;
    }

    [ExcludeFromCodeCoverage]
    public async Task ExecuteAsync(Func<IDatabaseExecutor, Task> action, CancellationToken ct = default)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            var executor = new TransactionalExecutor(conn, _options);
            await action(executor);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}

[ExcludeFromCodeCoverage]
internal sealed class TransactionalExecutor : IDatabaseExecutor
{
    private readonly NpgsqlConnection _conn;
    private readonly HellnetDatabaseOptions _options;

    public TransactionalExecutor(NpgsqlConnection conn, HellnetDatabaseOptions options)
    {
        _conn = conn;
        _options = options;
    }

    public Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.ExecuteAsync(new CommandDefinition(sql, parameters, transaction: null,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction: null,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct)).ContinueWith(t => (IReadOnlyList<T>)t.Result.AsList(), ct);

    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, parameters, transaction: null,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
        => _conn.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, transaction: null,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
}
