namespace Hellnet.Database.Abstractions;

public interface IDatabaseHealthChecker
{
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
    Task<DatabaseHealthReport> GetHealthAsync(CancellationToken ct = default);
}

public sealed record DatabaseHealthReport(
    bool IsHealthy,
    string Provider,
    string Version,
    TimeSpan Uptime,
    long OpenConnections,
    long? PoolWaitCount,
    string? ErrorMessage);
