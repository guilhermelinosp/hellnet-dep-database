using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.HealthChecks;
using Hellnet.Database.Observability;
using Hellnet.Database.PostgreSql;
using Hellnet.Database.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hellnet.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddHellnetDatabase(this IServiceCollection services)
    {
        var options = DatabaseEnvBinder.Bind();
        DatabaseEnvBinder.Validate(options);
        return services.AddHellnetDatabase(options);
    }

    public static IServiceCollection AddHellnetDatabase(
        this IServiceCollection services,
        HellnetDatabaseOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IDatabaseConnectionFactory, PostgresConnectionFactory>();
        services.AddSingleton<IDatabaseExecutor>(sp =>
            sp.GetRequiredService<IDatabaseConnectionFactory>().CreateExecutor());
        services.AddSingleton<IDatabaseTransaction>(sp =>
            sp.GetRequiredService<IDatabaseConnectionFactory>().CreateTransaction());
        services.AddSingleton<DatabaseRetryPolicy>();
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var opts = sp.GetRequiredService<HellnetDatabaseOptions>();
            return new NpgsqlDataSourceBuilder(opts.BuildConnectionString()).Build();
        });

        if (options.EnableHealthCheck)
            services.AddSingleton<IDatabaseHealthChecker, PostgresHealthChecker>();

        // Register IRepository<T> open generic
        services.AddTransient(typeof(IRepository<>), typeof(PostgresRepository<>));

        return services;
    }

    /// <summary>
    /// Registers Hellnet.Database metrics in OpenTelemetry MeterProviderBuilder.
    /// Usage: builder.Services.AddHellnetMetrics(metrics => metrics.AddHellnetDatabaseMetrics());
    /// </summary>
    public static OpenTelemetry.Metrics.MeterProviderBuilder AddHellnetDatabaseMetrics(
        this OpenTelemetry.Metrics.MeterProviderBuilder builder)
    {
        DatabaseMetrics.AddMeter(builder);
        return builder;
    }
}
