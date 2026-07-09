namespace Hellnet.Database.Configuration;

internal static class DatabaseEnvBinder
{
    public static HellnetDatabaseOptions Bind()
    {
        return new HellnetDatabaseOptions
        {
            Host = Env("HELLNET_DATABASE_HOST", "localhost"),
            Port = EnvInt("HELLNET_DATABASE_PORT", 5432),
            Database = Env("HELLNET_DATABASE_NAME", string.Empty),
            Username = Env("HELLNET_DATABASE_USERNAME", string.Empty),
            Password = Env("HELLNET_DATABASE_PASSWORD", string.Empty),
            PoolMinSize = EnvInt("HELLNET_DATABASE_POOL_MIN_SIZE", 10),
            PoolMaxSize = EnvInt("HELLNET_DATABASE_POOL_MAX_SIZE", 100),
            CommandTimeoutSeconds = EnvInt("HELLNET_DATABASE_COMMAND_TIMEOUT_SECONDS", 30),
            ConnectionTimeoutSeconds = EnvInt("HELLNET_DATABASE_CONNECTION_TIMEOUT_SECONDS", 15),
            RetryEnabled = EnvBool("HELLNET_DATABASE_RETRY_ENABLED", true),
            RetryMaxCount = EnvInt("HELLNET_DATABASE_RETRY_MAX_COUNT", 3),
            RetryBaseDelayMs = EnvInt("HELLNET_DATABASE_RETRY_BASE_DELAY_MS", 100),
            SlowQueryMs = EnvInt("HELLNET_DATABASE_SLOW_QUERY_MS", 500),
        };
    }

    internal static void Validate(HellnetDatabaseOptions options)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Database))
            missing.Add("HELLNET_DATABASE_NAME");
        if (string.IsNullOrWhiteSpace(options.Username))
            missing.Add("HELLNET_DATABASE_USERNAME");
        if (string.IsNullOrWhiteSpace(options.Password))
            missing.Add("HELLNET_DATABASE_PASSWORD");
        if (options.PoolMaxSize <= 0)
            missing.Add("HELLNET_DATABASE_POOL_MAX_SIZE (must be > 0)");

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
