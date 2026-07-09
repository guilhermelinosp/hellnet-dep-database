namespace Hellnet.Database.Abstractions;

/// <summary>Transaction scope. Handles begin/commit/rollback.</summary>
public interface IDatabaseTransaction
{
    Task ExecuteAsync(Func<IDatabaseExecutor, Task> action, CancellationToken ct = default);
}
