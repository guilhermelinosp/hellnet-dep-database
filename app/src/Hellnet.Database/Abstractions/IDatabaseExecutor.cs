namespace Hellnet.Database.Abstractions;

/// <summary>Generic database executor. No ORM, no provider coupling.</summary>
public interface IDatabaseExecutor
{
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
}
