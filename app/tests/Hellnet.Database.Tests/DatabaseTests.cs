using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hellnet.Database.Tests;

public sealed record TestMessage
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

// ============================================================
// DatabaseEnvBinder tests
// ============================================================

public sealed class DatabaseEnvBinderTests : IDisposable
{
    public DatabaseEnvBinderTests() => ClearEnv();
    public void Dispose() => ClearEnv();

    private static void ClearEnv()
    {
        foreach (var key in new[]
        {
            "HELLNET_DATABASE_CONNECTION_STRING", "HELLNET_DATABASE_PROVIDER",
            "HELLNET_DATABASE_MAX_POOL_SIZE", "HELLNET_DATABASE_CONNECTION_TIMEOUT",
            "HELLNET_DATABASE_COMMAND_TIMEOUT", "HELLNET_DATABASE_ENABLE_HEALTH_CHECK",
            "HELLNET_DATABASE_MAX_RETRY_COUNT", "HELLNET_DATABASE_RETRY_BASE_DELAY_MS",
            "HELLNET_DATABASE_ENABLE_COMMAND_LOGGING",
        })
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Bind_UsesDefaults_WhenNoEnvSet()
    {
        var options = DatabaseEnvBinder.Bind();
        Assert.Empty(options.ConnectionString);
        Assert.Equal("postgresql", options.Provider);
        Assert.Equal(50, options.MaxPoolSize);
        Assert.Equal(15, options.ConnectionTimeout);
        Assert.Equal(30, options.CommandTimeout);
        Assert.True(options.EnableHealthCheck);
        Assert.Equal(3, options.MaxRetryCount);
    }

    [Fact]
    public void Bind_ReadsFromEnv()
    {
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_CONNECTION_STRING", "Host=db;Port=5432;Database=test");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_PROVIDER", "postgresql");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_MAX_POOL_SIZE", "25");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_ENABLE_HEALTH_CHECK", "false");
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_MAX_RETRY_COUNT", "5");

        var options = DatabaseEnvBinder.Bind();
        Assert.Equal("Host=db;Port=5432;Database=test", options.ConnectionString);
        Assert.Equal(25, options.MaxPoolSize);
        Assert.False(options.EnableHealthCheck);
        Assert.Equal(5, options.MaxRetryCount);
    }

    [Fact]
    public void Validate_Throws_WhenConnectionStringMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions()));
        Assert.Contains("HELLNET_DATABASE_CONNECTION_STRING", ex.Message);
    }

    [Fact]
    public void Validate_Throws_WhenMaxPoolSizeInvalid()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
            {
                ConnectionString = "Host=db",
                MaxPoolSize = 0,
            }));
        Assert.Contains("MAX_POOL_SIZE", ex.Message);
    }

    [Fact]
    public void Validate_Success_WhenAllValid()
    {
        DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
        {
            ConnectionString = "Host=db",
            MaxPoolSize = 10,
            CommandTimeout = 30,
        });
    }
}

// ============================================================
// PostgresDatabase tests
// ============================================================

public sealed class PostgresDatabaseTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var options = new HellnetDatabaseOptions
        {
            ConnectionString = "Host=localhost;Database=test",
        };
        var logger = NullLogger<PostgresDatabase>.Instance;
        var db = new PostgresDatabase(options, logger);
        Assert.NotNull(db);
    }

    [Fact]
    public async Task HealthCheck_ReturnsFalse_WhenNoDatabase()
    {
        var options = new HellnetDatabaseOptions
        {
            ConnectionString = "Host=nonexistent;Database=test",
            ConnectionTimeout = 1,
        };
        var logger = NullLogger<PostgresDatabase>.Instance;
        var db = new PostgresDatabase(options, logger);

        var result = await db.IsHealthyAsync(default);
        Assert.False(result);
    }

    [Fact]
    public async Task GetHealth_ReturnsSnapshot()
    {
        var options = new HellnetDatabaseOptions
        {
            ConnectionString = "Host=localhost;Database=test",
        };
        var logger = NullLogger<PostgresDatabase>.Instance;
        var db = new PostgresDatabase(options, logger);

        var health = await db.GetHealthAsync(default);
        Assert.Equal("postgresql", health.Provider);
    }
}

// ============================================================
// DependencyInjection tests
// ============================================================

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddHellnetDatabase_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddHellnetDatabase(new HellnetDatabaseOptions
        {
            ConnectionString = "Host=localhost",
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
            ConnectionString = "Host=localhost",
        });

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<HellnetDatabaseOptions>());
        Assert.NotNull(sp.GetRequiredService<IDatabase>());
        Assert.NotNull(sp.GetRequiredService<IDatabaseHealthCheck>());
    }

    [Fact]
    public void AddHellnetDatabase_WithoutHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(new HellnetDatabaseOptions
        {
            ConnectionString = "Host=localhost",
            EnableHealthCheck = false,
        });

        using var sp = services.BuildServiceProvider();
        Assert.Null(sp.GetService<IDatabaseHealthCheck>());
    }

    [Fact]
    public void AddHellnetDatabase_Throws_WhenConnectionStringMissing()
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddHellnetDatabase());
    }

    [Fact]
    public void AddHellnetDatabase_UsesEnv_WhenNoOptions()
    {
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_CONNECTION_STRING", "Host=from-env");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase();
        using var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<HellnetDatabaseOptions>();
        Assert.Equal("Host=from-env", opts.ConnectionString);
        Environment.SetEnvironmentVariable("HELLNET_DATABASE_CONNECTION_STRING", null);
    }
}

// ============================================================
// HellnetDatabaseOptions tests
// ============================================================

public sealed class HellnetDatabaseOptionsTests
{
    [Fact]
    public void Defaults_AreSane()
    {
        var opts = new HellnetDatabaseOptions();
        Assert.Empty(opts.ConnectionString);
        Assert.Equal("postgresql", opts.Provider);
        Assert.True(opts.EnableHealthCheck);
        Assert.Equal(3, opts.MaxRetryCount);
    }
}
