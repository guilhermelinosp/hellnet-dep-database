using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Hellnet.Database.PostgreSql;
using Hellnet.Database.Resilience;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Hellnet.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddHellnetDatabase(this IServiceCollection services)
    {
        HellnetDatabaseOptions options = DatabaseEnvBinder.Bind();
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
            HellnetDatabaseOptions opts = sp.GetRequiredService<HellnetDatabaseOptions>();
            return new NpgsqlDataSourceBuilder(opts.BuildConnectionString()).Build();
        });

        // Register IRepository<T> open generic
        services.AddTransient(typeof(IRepository<>), typeof(PostgresRepository<>));

        return services;
    }
}
