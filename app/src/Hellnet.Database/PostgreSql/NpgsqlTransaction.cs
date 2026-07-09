using System.Diagnostics.CodeAnalysis;

using Dapper;

using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Hellnet.Database.PostgreSql;

internal sealed class NpgsqlTransaction(
    NpgsqlDataSource dataSource,
    HellnetDatabaseOptions options,
    ILogger<NpgsqlTransaction> logger) : IDatabaseTransaction
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly HellnetDatabaseOptions _options = options;
    private readonly ILogger<NpgsqlTransaction> _logger = logger;

    [ExcludeFromCodeCoverage]
    public async Task ExecuteAsync(Func<IDatabaseExecutor, Task> action, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        await using Npgsql.NpgsqlTransaction tx = await conn.BeginTransactionAsync(ct);
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
internal sealed class TransactionalExecutor(NpgsqlConnection conn, HellnetDatabaseOptions options) : IDatabaseExecutor
{
    private readonly NpgsqlConnection _conn = conn;
    private readonly HellnetDatabaseOptions _options = options;

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
