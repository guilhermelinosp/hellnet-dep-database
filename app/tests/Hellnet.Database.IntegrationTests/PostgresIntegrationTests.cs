using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hellnet.Database.IntegrationTests;

/// <summary>
/// Integration tests against a real PostgreSQL instance.
/// Requires HELLNET_DATABASE_CONNECTION_STRING env var set.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PostgresIntegrationTests
{
    private static HellnetDatabaseOptions GetOptions()
    {
        var connString = Environment.GetEnvironmentVariable("HELLNET_DATABASE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connString))
        {
            return null!; // Skip — no connection string
        }

        return new HellnetDatabaseOptions
        {
            ConnectionString = connString,
            ConnectionTimeout = 5,
        };
    }

    private static bool HasConnectionString()
        => !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("HELLNET_DATABASE_CONNECTION_STRING"));

    [Fact(Skip = "Requires real PostgreSQL. Set HELLNET_DATABASE_CONNECTION_STRING to enable.")]
    public async Task HealthCheck_ReturnsTrue_WhenConnected()
    {
        // Arrange
        var options = GetOptions();
        var logger = NullLogger<PostgresDatabase>.Instance;
        var db = new PostgresDatabase(options, logger);

        // Act
        var healthy = await db.IsHealthyAsync(default);

        // Assert
        Assert.True(healthy);
    }

    [Fact(Skip = "Requires real PostgreSQL. Set HELLNET_DATABASE_CONNECTION_STRING to enable.")]
    public async Task ExecuteScalar_ReturnsOne()
    {
        // Arrange
        var options = GetOptions();
        var logger = NullLogger<PostgresDatabase>.Instance;
        var db = new PostgresDatabase(options, logger);

        // Act
        var result = await db.ExecuteScalarAsync<int>("SELECT 1");

        // Assert
        Assert.Equal(1, result);
    }

    [Fact(Skip = "Requires real PostgreSQL. Set HELLNET_DATABASE_CONNECTION_STRING to enable.")]
    public async Task Query_ReturnsResults()
    {
        // Arrange
        var options = GetOptions();
        var logger = NullLogger<PostgresDatabase>.Instance;
        var db = new PostgresDatabase(options, logger);

        // Act
        var results = await db.QueryAsync<TestRow>("SELECT 1 as Id, 'test' as Name");

        // Assert
        var row = Assert.Single(results);
        Assert.Equal(1, row.Id);
        Assert.Equal("test", row.Name);
    }

    public sealed record TestRow
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
