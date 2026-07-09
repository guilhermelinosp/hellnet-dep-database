namespace Hellnet.Database.Abstractions;

/// <summary>
/// Health check for database connectivity. Used by K8s probes.
/// </summary>
public interface IDatabaseHealthCheck
{
    /// <summary>Ping the database. Returns true if reachable.</summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);

    /// <summary>Detailed health info.</summary>
    Task<DatabaseHealth> GetHealthAsync(CancellationToken ct = default);
}

/// <summary>Health status snapshot.</summary>
public sealed record DatabaseHealth(
    bool IsHealthy,
    string Provider,
    TimeSpan Uptime,
    int OpenConnections,
    string? ErrorMessage = null);
