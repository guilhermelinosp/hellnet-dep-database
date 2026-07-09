using AutoFixture;
using FluentAssertions;
using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hellnet.Database.UnitTests;

public sealed class DatabaseEnvBinderTests : IDisposable
{
    private readonly Fixture _fixture = new();

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

        opts.Host.Should().Be("localhost");
        opts.Port.Should().Be(5432);
        opts.Database.Should().BeEmpty();
        opts.Username.Should().BeEmpty();
        opts.Password.Should().BeEmpty();
        opts.PoolMinSize.Should().Be(10);
        opts.PoolMaxSize.Should().Be(100);
        opts.CommandTimeoutSeconds.Should().Be(30);
        opts.RetryEnabled.Should().BeTrue();
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

        opts.Host.Should().Be("pg.hellnet.com.br");
        opts.Port.Should().Be(5433);
        opts.Database.Should().Be("orders");
        opts.Username.Should().Be("app");
        opts.Password.Should().Be("secret");
        opts.PoolMaxSize.Should().Be(50);
        opts.RetryEnabled.Should().BeFalse();
    }

    [Fact]
    public void Validate_Throws_WhenNameMissing()
    {
        var act = () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*HELLNET_DATABASE_NAME*");
    }

    [Fact]
    public void Validate_Throws_WhenPoolSizeInvalid()
    {
        var act = () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
            PoolMaxSize = 0,
        });
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*POOL_MAX_SIZE*");
    }

    [Fact]
    public void Validate_Success_WhenAllValid()
    {
        var act = () => DatabaseEnvBinder.Validate(new HellnetDatabaseOptions
        {
            Database = "test",
            Username = "u",
            Password = "p",
        });
        act.Should().NotThrow();
    }
}

public sealed class HellnetDatabaseOptionsTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void BuildConnectionString_IncludesAllFields()
    {
        var opts = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.ConnectionTimeoutSeconds, 10)
            .Create();

        var cs = opts.BuildConnectionString();

        cs.Should().Contain($"Host={opts.Host}");
        cs.Should().Contain($"Port={opts.Port}");
        cs.Should().Contain($"Database={opts.Database}");
        cs.Should().Contain($"Username={opts.Username}");
        cs.Should().Contain($"Password={opts.Password}");
        cs.Should().Contain("Minimum Pool Size=");
        cs.Should().Contain("Maximum Pool Size=");
    }
}

public sealed class DatabaseRetryPolicyTests
{
    private static readonly ILogger<DatabaseRetryPolicy> _logger = NullLogger<DatabaseRetryPolicy>.Instance;
    private readonly Fixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_Success_WithoutRetry()
    {
        var options = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.Database, "test")
            .With(x => x.Username, "u")
            .With(x => x.Password, "p")
            .Create();
        var policy = new DatabaseRetryPolicy(options, _logger);

        var result = await policy.ExecuteAsync(() => Task.FromResult(42), default);

        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_Retries_OnTransientError()
    {
        var options = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.RetryEnabled, true)
            .With(x => x.RetryMaxCount, 1)
            .With(x => x.RetryBaseDelayMs, 1)
            .Create();
        var policy = new DatabaseRetryPolicy(options, _logger);
        var calls = 0;

        var act = () => policy.ExecuteAsync<object>(() =>
        {
            calls++;
            throw new TimeoutException("transient");
        }, default);

        await act.Should().ThrowAsync<TimeoutException>();
        calls.Should().Be(2); // original + 1 retry
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_PermanentError()
    {
        var options = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.RetryEnabled, true)
            .With(x => x.RetryMaxCount, 3)
            .With(x => x.RetryBaseDelayMs, 1)
            .Create();
        var policy = new DatabaseRetryPolicy(options, _logger);
        var calls = 0;

        var act = () => policy.ExecuteAsync<object>(() =>
        {
            calls++;
            throw new Npgsql.PostgresException("", "", "", "42601", "");
        }, default);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
        calls.Should().Be(1); // no retry
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetry_WhenDisabled()
    {
        var options = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.RetryEnabled, false)
            .Create();
        var policy = new DatabaseRetryPolicy(options, _logger);
        var calls = 0;

        var act = () => policy.ExecuteAsync<object>(() =>
        {
            calls++;
            throw new TimeoutException();
        }, default);

        await act.Should().ThrowAsync<TimeoutException>();
        calls.Should().Be(1);
    }
}

[Collection("Sequential")]
public sealed class DependencyInjectionTests
{
    private readonly Fixture _fixture = new();

    [Fact]
    public void AddHellnetDatabase_ReturnsServiceCollection()
    {
        var options = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.Database, "test")
            .With(x => x.Username, "u")
            .With(x => x.Password, "p")
            .Create();
        var services = new ServiceCollection();

        var result = services.AddHellnetDatabase(options);

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddHellnetDatabase_RegistersCoreServices()
    {
        var options = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.Database, "test")
            .With(x => x.Username, "u")
            .With(x => x.Password, "p")
            .Create();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(options);

        using var sp = services.BuildServiceProvider();

        sp.GetRequiredService<HellnetDatabaseOptions>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseExecutor>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseTransaction>().Should().NotBeNull();
        sp.GetRequiredService<IDatabaseConnectionFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddHellnetDatabase_Throws_WhenMissingRequired()
    {
        var services = new ServiceCollection();

        var act = () => services.AddHellnetDatabase();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddHellnetDatabase_WithOptions_HasCorrectValues()
    {
        var expected = _fixture.Build<HellnetDatabaseOptions>()
            .With(x => x.Database, "testdb")
            .With(x => x.Username, "user")
            .With(x => x.Password, "pass")
            .With(x => x.Host, "pg.internal")
            .Create();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHellnetDatabase(expected);

        using var sp = services.BuildServiceProvider();
        var actual = sp.GetRequiredService<HellnetDatabaseOptions>();

        actual.Database.Should().Be(expected.Database);
        actual.Username.Should().Be(expected.Username);
        actual.Host.Should().Be(expected.Host);
    }
}
