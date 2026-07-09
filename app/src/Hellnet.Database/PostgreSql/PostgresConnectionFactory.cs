using Hellnet.Database.Abstractions;
using Hellnet.Database.Configuration;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Hellnet.Database.PostgreSql;

internal sealed class PostgresConnectionFactory(HellnetDatabaseOptions options, ILoggerFactory loggerFactory) : IDatabaseConnectionFactory, IAsyncDisposable, IDisposable
{
    private readonly NpgsqlDataSource _dataSource = new NpgsqlDataSourceBuilder(options.BuildConnectionString()).Build();
    private readonly HellnetDatabaseOptions _options = options;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

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
