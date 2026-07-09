using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hellnet.Database.PostgreSql;

internal sealed class PostgresConnectionFactory : IDatabaseConnectionFactory, IAsyncDisposable, IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly HellnetDatabaseOptions _options;
    private readonly ILoggerFactory _loggerFactory;

    public PostgresConnectionFactory(HellnetDatabaseOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
        _dataSource = new NpgsqlDataSourceBuilder(options.BuildConnectionString()).Build();
    }

    public IDatabaseExecutor CreateExecutor()
        => new NpgsqlExecutor(_dataSource, _options,
            _loggerFactory.CreateLogger<NpgsqlExecutor>());

    public IDatabaseTransaction CreateTransaction()
        => new NpgsqlTransaction(_dataSource, _options,
            _loggerFactory.CreateLogger<NpgsqlTransaction>());

    // Exposed for health check and diagnostics
    internal NpgsqlDataSource DataSource => _dataSource;
    internal HellnetDatabaseOptions Options => _options;

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }

    void IDisposable.Dispose()
    {
        _dataSource.Dispose();
    }
}
