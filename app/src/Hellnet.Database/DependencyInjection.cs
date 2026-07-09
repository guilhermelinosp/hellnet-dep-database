using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Hellnet.Database;

/// <summary>
/// Extension methods for registering Hellnet.Database services in DI.
/// Env-first: reads HELLNET_DATABASE_* environment variables automatically.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Register database services using environment variables.</summary>
    public static IServiceCollection AddHellnetDatabase(this IServiceCollection services)
    {
        var options = DatabaseEnvBinder.Bind();
        DatabaseEnvBinder.Validate(options);
        return services.AddHellnetDatabase(options);
    }

    /// <summary>Register database services with explicit options.</summary>
    public static IServiceCollection AddHellnetDatabase(
        this IServiceCollection services,
        HellnetDatabaseOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IDatabase>(sp =>
        {
            var opts = sp.GetRequiredService<HellnetDatabaseOptions>();
            var logger = sp.GetRequiredService<ILogger<PostgresDatabase>>();
            return new PostgresDatabase(opts, logger);
        });

        if (options.EnableHealthCheck)
        {
            services.AddSingleton<IDatabaseHealthCheck>(sp =>
                (PostgresDatabase)sp.GetRequiredService<IDatabase>());
        }

        return services;
    }
}
