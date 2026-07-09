using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.Resilience;
using Microsoft.Extensions.DependencyInjection;
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

        var opts = DatabaseEnvBinder.Bind();

        Assert.Equal("pg.hellnet.com.br", opts.Host);
        Assert.Equal(5433, opts.Port);
        Assert.Equal("orders", opts.Database);
        Assert.Equal("app", opts.Username);
        Assert.Equal("secret", opts.Password);
        Assert.Equal(50, opts.PoolMaxSize);
        Assert.False(opts.RetryEnabled);
    }

    [Fact]
    public void Validate_Throws_WhenNameMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions()));
        Assert.Contains("HELLNET_DATABASE_NAME", ex.Message);
    }

    [Fact]
    public void Validate_Throws_WhenRequiredFieldsMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions()));
        Assert.Contains("HELLNET_DATABASE_NAME", ex.Message);
        Assert.Contains("HELLNET_DATABASE_USERNAME", ex.Message);
        Assert.Contains("HELLNET_DATABASE_PASSWORD", ex.Message);
    }

    [Fact]
    public void Validate_Throws_WhenPoolSizeInvalid()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
            {
                Database = "test",
                Username = "u",
                Password = "p",
                PoolMaxSize = 0,
            }));
        Assert.Contains("POOL_MAX_SIZE", ex.Message);
    }

    [Fact]
    public void Validate_Success_WhenAllValid()
    {
        DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
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
            Host = "db.example.com",
            Port = 5555,
            Database = "mydb",
            Username = "admin",
            Password = "s3cret",
            PoolMinSize = 5,
            PoolMaxSize = 50,
            CommandTimeoutSeconds = 60,
            ConnectionTimeoutSeconds = 30,
        };

        var cs = opts.BuildConnectionString();

        Assert.Contains("Host=db.example.com", cs);
        Assert.Contains("Port=5555", cs);
        Assert.Contains("Database=mydb", cs);
        Assert.Contains("Username=admin", cs);
        Assert.Contains("Password=s3cret", cs);
        Assert.Contains("Minimum Pool Size=5", cs);
        Assert.Contains("Maximum Pool Size=50", cs);
        Assert.Contains("Command Timeout=60", cs);
        Assert.Contains("Timeout=30", cs);
    }

    [Fact]
    public void BuildConnectionString_UsesDefaults()
    {
        var opts = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
        };

        var cs = opts.BuildConnectionString();

        Assert.Contains("Host=localhost", cs);
        Assert.Contains("Port=5432", cs);
        Assert.Contains("Minimum Pool Size=10", cs);
        Assert.Contains("Maximum Pool Size=100", cs);
        Assert.Contains("Command Timeout=30", cs);
        Assert.Contains("Timeout=15", cs);
    }

    [Fact]
    public void BuildConnectionString_IncludesPoolSettings()
    {
        var opts = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            PoolMinSize = 10,
            PoolMaxSize = 50,
        };

        var cs = opts.BuildConnectionString();

        Assert.Contains("Minimum Pool Size=10", cs);
        Assert.Contains("Maximum Pool Size=50", cs);
        Assert.Contains("Pooling=True", cs);
    }
}

public sealed class DatabaseRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_Success_WithoutRetry()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            RetryEnabled = true,
            RetryMaxCount = 1,
            RetryBaseDelayMs = 1,
        };
        var policy = new DatabaseRetryPolicy(options, NullLogger<DatabaseRetryPolicy>.Instance);

        var result = await policy.ExecuteAsync(() => Task.FromResult(42), default);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task ExecuteAsync_Retries_OnTransientError()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            RetryEnabled = true,
            RetryMaxCount = 1,
            RetryBaseDelayMs = 1,
        };
        var policy = new DatabaseRetryPolicy(options, NullLogger<DatabaseRetryPolicy>.Instance);
        var calls = 0;

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
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
            Database = "test",
            Username = "u",
            Password = "p",
            RetryEnabled = true,
            RetryMaxCount = 3,
            RetryBaseDelayMs = 1,
        };
        var policy = new DatabaseRetryPolicy(options, NullLogger<DatabaseRetryPolicy>.Instance);
        var calls = 0;

        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            policy.ExecuteAsync<object>(() =>
            {
                calls++;
                throw new Npgsql.PostgresException("", "", "", "42601", "");
            }, default));

        Assert.Equal(1, calls); // no retry
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_WhenDisabled()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            RetryEnabled = false,
        };
        var policy = new DatabaseRetryPolicy(options, NullLogger<DatabaseRetryPolicy>.Instance);
        var calls = 0;

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<object>(() =>
            {
                calls++;
                throw new TimeoutException();
            }, default));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_ActionOverload_Success()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            RetryEnabled = true,
            RetryMaxCount = 1,
            RetryBaseDelayMs = 1,
        };
        var policy = new DatabaseRetryPolicy(options, NullLogger<DatabaseRetryPolicy>.Instance);
        var executed = false;

        await policy.ExecuteAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        }, default);

        Assert.True(executed);
    }
}

[Collection("Sequential")]
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddHellnetDatabase_ReturnsServiceCollection()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            PoolMinSize = 10,
            PoolMaxSize = 100,
        };
        var services = new ServiceCollection();

        var result = services.AddHellnetDatabase(options);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddHellnetDatabase_RegistersCoreServices()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            PoolMinSize = 10,
            PoolMaxSize = 100,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(options);

        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<HellnetDatabaseOptions>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseExecutor>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseTransaction>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseConnectionFactory>());
    }

    [Fact]
    public void AddHellnetDatabase_Throws_WhenMissingRequired()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddHellnetDatabase());

        Assert.Contains("HELLNET_DATABASE_NAME", ex.Message);
    }

    [Fact]
    public void AddHellnetDatabase_WithOptions_HasCorrectValues()
    {
        var expected = new HellnetDatabaseOptions
        {
            Database = "testdb",
            Username = "user",
            Password = "pass",
            Host = "pg.internal",
            PoolMinSize = 10,
            PoolMaxSize = 100,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(expected);

        using var sp = services.BuildServiceProvider();
        var actual = sp.GetRequiredService<HellnetDatabaseOptions>();

        Assert.Equal(expected.Database, actual.Database);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.Host, actual.Host);
    }

    [Fact]
    public void AddHellnetDatabase_RegistersRepository()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            PoolMinSize = 10,
            PoolMaxSize = 100,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(options);

        using var sp = services.BuildServiceProvider();

        var repo = sp.GetService<IRepository<object>>();
        Assert.NotNull(repo);
    }

    [Fact]
    public void AddHellnetDatabase_RegistersRetryPolicy()
    {
        var options = new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            PoolMinSize = 10,
            PoolMaxSize = 100,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(options);

        using var sp = services.BuildServiceProvider();

        var policy = sp.GetService<DatabaseRetryPolicy>();
        Assert.NotNull(policy);
    }
}
