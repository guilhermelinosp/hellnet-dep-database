using System.Data;

namespace Hellnet.Database.Abstractions;

/// <summary>
/// Main database abstraction. Env-first, no connection string in code.
/// </summary>
public interface IDatabase
{
    /// <summary>Execute a command and return the number of affected rows.</summary>
    Task<int> ExecuteAsync(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Query and map results to typed objects.</summary>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Query a single row or default.</summary>
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Execute a command and return the first column of the first row as a scalar value.</summary>
    Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);

    /// <summary>Execute multiple statements in a transaction.</summary>
    Task TransactionAsync(Func<IDatabase, Task> action, CancellationToken ct = default);
}
