namespace Hellnet.Database.Configuration;

/// <summary>
/// Reads HellnetDatabaseOptions from HELLNET_DATABASE_* environment variables.
/// Env-first: no appsettings.json, no IConfiguration.
/// </summary>
internal static class DatabaseEnvBinder
{
    public static HellnetDatabaseOptions Bind()
    {
        return new HellnetDatabaseOptions
        {
            ConnectionString = Env("HELLNET_DATABASE_CONNECTION_STRING", string.Empty),
            Provider = Env("HELLNET_DATABASE_PROVIDER", "postgresql"),
            MaxPoolSize = EnvInt("HELLNET_DATABASE_MAX_POOL_SIZE", 50),
            ConnectionTimeout = EnvInt("HELLNET_DATABASE_CONNECTION_TIMEOUT", 15),
            CommandTimeout = EnvInt("HELLNET_DATABASE_COMMAND_TIMEOUT", 30),
            EnableHealthCheck = EnvBool("HELLNET_DATABASE_ENABLE_HEALTH_CHECK", true),
            MaxRetryCount = EnvInt("HELLNET_DATABASE_MAX_RETRY_COUNT", 3),
            RetryBaseDelayMs = EnvInt("HELLNET_DATABASE_RETRY_BASE_DELAY_MS", 100),
            EnableCommandLogging = EnvBool("HELLNET_DATABASE_ENABLE_COMMAND_LOGGING", false),
        };
    }

    internal static void Validate(HellnetDatabaseOptions options)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            missing.Add("HELLNET_DATABASE_CONNECTION_STRING");
        if (options.MaxPoolSize <= 0)
            missing.Add("HELLNET_DATABASE_MAX_POOL_SIZE (must be > 0)");
        if (options.CommandTimeout <= 0)
            missing.Add("HELLNET_DATABASE_COMMAND_TIMEOUT (must be > 0)");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Missing or invalid environment variables: {string.Join(", ", missing)}");
    }

    internal static string Env(string key, string fallback)
        => Environment.GetEnvironmentVariable(key) ?? fallback;

    internal static string? EnvOrNull(string key)
        => Environment.GetEnvironmentVariable(key);

    internal static bool EnvBool(string key, bool fallback)
        => bool.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;

    internal static int EnvInt(string key, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;
}
