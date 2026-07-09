namespace Hellnet.Database.Configuration;

/// <summary>
/// Options for Hellnet.Database. Populated from HELLNET_DATABASE_* env vars.
/// </summary>
public sealed class HellnetDatabaseOptions
{
    /// <summary>Connection string. Env: HELLNET_DATABASE_CONNECTION_STRING. Required.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>Database provider: postgresql (default) or sqlserver. Env: HELLNET_DATABASE_PROVIDER.</summary>
    public string Provider { get; init; } = "postgresql";

    /// <summary>Max connection pool size. Env: HELLNET_DATABASE_MAX_POOL_SIZE. Default: 50.</summary>
    public int MaxPoolSize { get; init; } = 50;

    /// <summary>Connection timeout in seconds. Env: HELLNET_DATABASE_CONNECTION_TIMEOUT. Default: 15.</summary>
    public int ConnectionTimeout { get; init; } = 15;

    /// <summary>Command timeout in seconds. Env: HELLNET_DATABASE_COMMAND_TIMEOUT. Default: 30.</summary>
    public int CommandTimeout { get; init; } = 30;

    /// <summary>Enable health check endpoint. Env: HELLNET_DATABASE_ENABLE_HEALTH_CHECK. Default: true.</summary>
    public bool EnableHealthCheck { get; init; } = true;

    /// <summary>Max retry count on transient errors. Env: HELLNET_DATABASE_MAX_RETRY_COUNT. Default: 3.</summary>
    public int MaxRetryCount { get; init; } = 3;

    /// <summary>Base delay in ms between retries (exponential backoff). Env: HELLNET_DATABASE_RETRY_BASE_DELAY_MS. Default: 100.</summary>
    public int RetryBaseDelayMs { get; init; } = 100;

    /// <summary>Enable command logging. Env: HELLNET_DATABASE_ENABLE_COMMAND_LOGGING. Default: false.</summary>
    public bool EnableCommandLogging { get; init; } = false;
}
