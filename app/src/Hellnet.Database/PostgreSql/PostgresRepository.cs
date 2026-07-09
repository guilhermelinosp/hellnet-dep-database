using System.Diagnostics.CodeAnalysis;

using Dapper;

using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Hellnet.Database.PostgreSql;

internal sealed class PostgresRepository<T>(
    NpgsqlDataSource dataSource,
    HellnetDatabaseOptions options,
    ILogger<PostgresRepository<T>> logger) : IRepository<T>
    where T : class
{
    private readonly NpgsqlDataSource _dataSource = dataSource;
    private readonly HellnetDatabaseOptions _options = options;
    private readonly ILogger<PostgresRepository<T>> _logger = logger;

    [ExcludeFromCodeCoverage]
    public async Task<T?> GetByIdAsync<TId>(TId id, CancellationToken ct = default)
    {
        var spec = new ByIdSpecification<T, TId>(id);
        return await QueryFirstOrDefault(spec.Sql, spec.Parameters, ct);
    }

    [ExcludeFromCodeCoverage]
    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        IEnumerable<T> result = await conn.QueryAsync<T>(new CommandDefinition(
            $"SELECT * FROM {typeof(T).Name}", cancellationToken: ct));
        return result.AsList();
    }

    [ExcludeFromCodeCoverage]
    public async Task<IReadOnlyList<T>> FindAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        IEnumerable<T> result = await conn.QueryAsync<T>(new CommandDefinition(spec.Sql, spec.Parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
        return result.AsList();
    }

    [ExcludeFromCodeCoverage]
    public async Task<PageResult<T>> PaginateAsync(ISpecification<T> spec, int page, int pageSize, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        var countSql = $"SELECT COUNT(*) FROM ({spec.Sql}) AS _count";
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"{spec.Sql} LIMIT {pageSize} OFFSET {(page - 1) * pageSize}",
            spec.Parameters, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

        IEnumerable<T> items = await conn.QueryAsync<T>(new CommandDefinition(
            spec.Sql, spec.Parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));

        return new PageResult<T>
        {
            Items = items.AsList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    [ExcludeFromCodeCoverage]
    public async Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*) FROM ({spec.Sql}) AS _count",
            spec.Parameters, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
    }

    private async Task<T?> QueryFirstOrDefault(string sql, object? parameters, CancellationToken ct)
    {
        await using NpgsqlConnection conn = await _dataSource.OpenConnectionAsync(ct);
        return await conn.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, parameters,
            commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: ct));
    }
}

[ExcludeFromCodeCoverage]
internal sealed record ByIdSpecification<T, TId>(TId Id) : ISpecification<T>
    where T : class
{
    public string Sql => $"SELECT * FROM {typeof(T).Name} WHERE Id = @Id";
    public object? Parameters => new { Id };
    public string? OrderBy => null;
}
