using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.HealthChecks;
using Hellnet.Database.PostgreSql;
using Hellnet.Database.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hellnet.Database.UnitTests;

public sealed class DatabaseEnvBinderTests : IDisposable
{
    public DatabaseEnvBinderTests() => ClearEnv();
    public void Dispose() => ClearEnv();

    private static void ClearEnv()
    {
        foreach (var key in new[]
        {
            "HELLNET_DATABASE_HOST", "HELLNET_DATABASE_PORT", "HELLNET_DATABASE_NAME",
            "HELLNET_DATABASE_USERNAME", "HELLNET_DATABASE_PASSWORD",
            "HELLNET_DATABASE_POOL_MIN_SIZE", "HELLNET_DATABASE_POOL_MAX_SIZE",
            "HELLNET_DATABASE_COMMAND_TIMEOUT_SECONDS", "HELLNET_DATABASE_CONNECTION_TIMEOUT_SECONDS",
            "HELLNET_DATABASE_RETRY_ENABLED", "HELLNET_DATABASE_RETRY_MAX_COUNT",
            "HELLNET_DATABASE_RETRY_BASE_DELAY_MS", "HELLNET_DATABASE_SLOW_QUERY_MS",
            "HELLNET_DATABASE_ENABLE_METRICS", "HELLNET_DATABASE_ENABLE_HEALTH_CHECK",
        })
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Bind_UsesDefaults_WhenNoEnvSet()
    {
        var opts = DatabaseEnvBinder.Bind();
        Assert.Equal("localhost", opts.Host);
        Assert.Equal(5432, opts.Port);
        Assert.Empty(opts.Database);
        Assert.Empty(opts.Username);
        Assert.Empty(opts.Password);
        Assert.Equal(10, opts.PoolMinSize);
        Assert.Equal(100, opts.PoolMaxSize);
        Assert.Equal(30, opts.CommandTimeoutSeconds);
        Assert.True(opts.RetryEnabled);
        Assert.True(opts.EnableMetrics);
    }

    [Fact]
    public void Bind_ReadsFromEnv()
    {
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_HOST", "pg.hellnet.com.br");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_PORT", "5433");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_NAME", "orders");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_USERNAME", "app");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_PASSWORD", "secret");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_POOL_MAX_SIZE", "50");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_RETRY_ENABLED", "false");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_ENABLE_METRICS", "false");

        var opts = DatabaseEnvBinder.Bind();
        Assert.Equal("pg.hellnet.com.br", opts.Host);
        Assert.Equal(5433, opts.Port);
        Assert.Equal("orders", opts.Database);
        Assert.Equal("app", opts.Username);
        Assert.Equal("secret", opts.Password);
        Assert.Equal(50, opts.PoolMaxSize);
        Assert.False(opts.RetryEnabled);
        Assert.False(opts.EnableMetrics);
    }

    [Fact]
    public void Validate_Throws_WhenNameMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions()));
        Assert.Contains("HELLNET_DATABASE_NAME", ex.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPoolSizeInvalid()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
            {
                Database = "test", Username = "u", Password = "p", PoolMaxSize = 0,
            }));
        Assert.Contains("POOL_MAX_SIZE", ex.Message);
    }

    [Fact]
    public void Validate_Success_WhenAllValid()
    {
        DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
        {
            Database = "test", Username = "u", Password = "p",
        });
    }
}

public sealed class HellnetDatabaseOptionsTests
{
    [Fact]
    public void BuildConnectionString_IncludesAllFields()
    {
        var opts = new HellnetDatabaseOptions
        {
            Host = "db.internal",
            Port = 5432,
            Database = "mydb",
            Username = "user",
            Password = "pass",
            PoolMinSize = 5,
            PoolMaxSize = 50,
            CommandTimeoutSeconds = 60,
            ConnectionTimeoutSeconds = 10,
        };

        var cs = opts.BuildConnectionString();
        Assert.Contains("Host=db.internal", cs);
        Assert.Contains("Port=5432", cs);
        Assert.Contains("Database=mydb", cs);
        Assert.Contains("Username=user", cs);
        Assert.Contains("Password=pass", cs);
        Assert.Contains("Minimum Pool Size=5", cs);
        Assert.Contains("Maximum Pool Size=50", cs);
    }
}

public sealed class DatabaseRetryPolicyTests
{
    private readonly ILogger<DatabaseRetryPolicy> _logger = NullLogger<DatabaseRetryPolicy>.Instance;

    [Fact]
    public async Task ExecuteAsync_Success_WithoutRetry()
    {
        var options = new HellnetDatabaseOptions { Database = "t", Username = "u", Password = "p" };
        var policy = new DatabaseRetryPolicy(options, _logger);
        var result = await policy.ExecuteAsync(() => Task.FromResult(42), default);
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_Retries_OnTransientError()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "t", Username = "u", Password = "p",
            RetryEnabled = true, RetryMaxCount = 1, RetryBaseDelayMs = 1,
        };
        var policy = new DatabaseRetryPolicy(options, _logger);
        var calls = 0;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<object>(() =>
            {
                calls++;
                throw new TimeoutException("transient");
            }, default));
        Assert.Equal(2, calls); // original + 1 retry
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_PermanentError()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "t", Username = "u", Password = "p",
            RetryEnabled = true, RetryMaxCount = 3, RetryBaseDelayMs = 1,
        };
        var policy = new DatabaseRetryPolicy(options, _logger);
        var calls = 0;

        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            policy.ExecuteAsync<object>(() =>
            {
                calls++;
                // Simulate a syntax error via PostgresException
                throw new Npgsql.PostgresException("", "", "", "42601", "");
            }, default));
        Assert.Equal(1, calls); // no retry
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_WhenDisabled()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "t", Username = "u", Password = "p",
            RetryEnabled = false,
        };
        var policy = new DatabaseRetryPolicy(options, _logger);
        var calls = 0;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<object>(() =>
            {
                calls++;
                throw new TimeoutException();
            }, default));
        Assert.Equal(1, calls);
    }
}

public sealed class PostgresHealthCheckerTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var options = new HellnetDatabaseOptions { Database = "t", Username = "u", Password = "p" };
        var logger = NullLogger<PostgresHealthChecker>.Instance;
        var builder = new Npgsql.NpgsqlDataSourceBuilder(options.BuildConnectionString());
        var ds = builder.Build();
        var checker = new PostgresHealthChecker(options, ds, logger);
        Assert.NotNull(checker);
        ds.Dispose();
    }
}
[Collection("Sequential")]
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddHellnetDatabase_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHellnetDatabase(new HellnetDatabaseOptions
        {
            Database = "test", Username = "u", Password = "p",
        });
        Assert.Same(services, result);
    }

    [Fact]
    public void AddHellnetDatabase_RegistersCoreServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(new HellnetDatabaseOptions
        {
            Database = "test", Username = "u", Password = "p",
        });

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<HellnetDatabaseOptions>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseExecutor>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseTransaction>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseConnectionFactory>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseHealthChecker>());
    }

    [Fact]
    public void AddHellnetDatabase_Throws_WhenMissingRequired()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddHellnetDatabase());
    }

    [Fact]
    public void AddHellnetDatabase_UsesEnv_WhenNoOptions()
    {
        // Set env vars immediately before Bind()
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_NAME", "from-env");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_USERNAME", "user");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_PASSWORD", "pass");
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHellnetDatabase();
            using var sp = services.BuildServiceProvider();
            var opts = sp.GetRequiredService<HellnetDatabaseOptions>();
            Assert.Equal("from-env", opts.Database);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HELLNET_DATABASE_NAME", null);
            Environment.SetEnvironmentVariable("HELLNET_DATABASE_USERNAME", null);
            Environment.SetEnvironmentVariable("HELLNET_DATABASE_PASSWORD", null);
        }
    }
}
