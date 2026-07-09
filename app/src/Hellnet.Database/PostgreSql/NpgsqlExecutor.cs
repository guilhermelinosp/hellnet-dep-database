using System.Diagnostics.CodeAnalysis;

using Dapper;

using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Hellnet.Database.PostgreSql;

internal sealed class NpgsqlExecutor(
    NpgsqlDataSource dataSource,
    HellnetDatabaseOptions options,
    ILogger<NpgsqlExecutor> logger) : IDatabaseExecutor
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly HellnetDatabaseOptions _options = options;
    private readonly ILogger<NpgsqlExecutor> _logger = logger;

    [ExcludeFromCodeCoverage]
    public async Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteAsync(new CommandDefinition(sql, parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
    }

    [ExcludeFromCodeCoverage]
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        IEnumerable<T> result = await conn.QueryAsync<T>(new CommandDefinition(sql, parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
        return result.AsList();
    }

    [ExcludeFromCodeCoverage]
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
    }

    [ExcludeFromCodeCoverage]
    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
    }
}
