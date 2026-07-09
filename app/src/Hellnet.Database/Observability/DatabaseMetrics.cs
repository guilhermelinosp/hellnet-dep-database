using System.Diagnostics.Metrics;

namespace Hellnet.Database.Observability;

/// <summary>
/// OpenTelemetry-compatible metrics for database operations.
/// Uses System.Diagnostics.Metrics — zero OTel dependency.
/// </summary>
internal static class DatabaseMetrics
{
    private static readonly Meter Meter = new("Hellnet.Database", "1.0.0");

    private static readonly Histogram<double> QueryDuration = Meter.CreateHistogram<double>(
        "hellnet.database.query.duration", "ms", "Query execution duration");

    private static readonly Counter<long> QueryCount = Meter.CreateCounter<long>(
        "hellnet.database.query.count", "{queries}", "Total query count");

    private static readonly Counter<long> QueryErrors = Meter.CreateCounter<long>(
        "hellnet.database.query.errors", "{errors}", "Query error count");

    private static readonly ObservableGauge<long> ConnectionActive = Meter.CreateObservableGauge<long>(
        "hellnet.database.connection.active", () => ActiveConnections, "Active connections");

    private static long ActiveConnections;

    public static void RecordQuery(double durationMs)
    {
        QueryDuration.Record(durationMs);
        QueryCount.Add(1);
    }

    public static void RecordError()
        => QueryErrors.Add(1);

    public static void ConnectionOpened()
        => Interlocked.Increment(ref ActiveConnections);

    public static void ConnectionClosed()
        => Interlocked.Decrement(ref ActiveConnections);

    public static void AddMeter(global::OpenTelemetry.Metrics.MeterProviderBuilder builder)
        => builder.AddMeter("Hellnet.Database");
}
