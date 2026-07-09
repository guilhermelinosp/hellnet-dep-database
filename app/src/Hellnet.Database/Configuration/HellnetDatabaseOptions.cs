namespace Hellnet.Database.Configuration;

/// <summary>Options built from individual HELLNET_DATABASE_* env vars.
/// Internally constructs the connection string. No connection string in code.</summary>
public sealed class HellnetDatabaseOptions
{
    // ── Connection ──────────────────────────────────────────────
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5432;
    public string Database { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    // ── Pool ────────────────────────────────────────────────────
    public int PoolMinSize { get; init; } = 10;
    public int PoolMaxSize { get; init; } = 100;

    // ── Timeouts ────────────────────────────────────────────────
    public int CommandTimeoutSeconds { get; init; } = 30;
    public int ConnectionTimeoutSeconds { get; init; } = 15;

    // ── Resilience ──────────────────────────────────────────────
    public bool RetryEnabled { get; init; } = true;
    public int RetryMaxCount { get; init; } = 3;
    public int RetryBaseDelayMs { get; init; } = 100;

    // ── Diagnostics ─────────────────────────────────────────────
    public int SlowQueryMs { get; init; } = 500;

    /// <summary>Builds the Npgsql connection string from individual fields.</summary>
    public string BuildConnectionString()
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = Host,
            Port = Port,
            Database = Database,
            Username = Username,
            Password = Password,
            MinPoolSize = PoolMinSize,
            MaxPoolSize = PoolMaxSize,
            CommandTimeout = CommandTimeoutSeconds,
            Timeout = ConnectionTimeoutSeconds,
            Pooling = true,
        };
        return builder.ConnectionString;
    }
}
