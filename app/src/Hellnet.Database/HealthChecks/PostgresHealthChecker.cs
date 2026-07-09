using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hellnet.Database.HealthChecks;

internal sealed class PostgresHealthChecker : IDatabaseHealthChecker
{
    private readonly HellnetDatabaseOptions _options;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresHealthChecker> _logger;
    private readonly DateTime _startedAt = DateTime.UtcNow;

    public PostgresHealthChecker(
        HellnetDatabaseOptions options,
        NpgsqlDataSource dataSource,
        ILogger<PostgresHealthChecker> logger)
    {
        _options = options;
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.CommandTimeout = 3;
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed");
            return false;
        }
    }

    public async Task<DatabaseHealthReport> GetHealthAsync(CancellationToken ct = default)
    {
        var healthy = false;
        var version = "";
        long openConnections = 0;

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT version()";
            version = (await cmd.ExecuteScalarAsync(ct) as string) ?? "";
            openConnections = 0; // NpgsqlDataSource statistics not available in all versions
            healthy = true;
        }
        catch (Exception ex)
        {
            return new DatabaseHealthReport(false, "postgresql", "", TimeSpan.Zero, 0, 0, ex.Message);
        }

        return new DatabaseHealthReport(healthy, "postgresql", version,
            DateTime.UtcNow - _startedAt, openConnections, 0, null);
    }
}
