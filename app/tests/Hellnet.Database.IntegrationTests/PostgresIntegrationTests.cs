using Hellnet.Database.Configuration;
using Hellnet.Database.PostgreSql;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Hellnet.Database.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class PostgresIntegrationTests
{
    [Fact(Skip = "Requires real PostgreSQL. Set HELLNET_DATABASE_NAME, _USERNAME, _PASSWORD to enable.")]
    public async Task Executor_Query_ReturnsResults()
    {
        var opts = DatabaseEnvBinder.Bind();
        var factory = new PostgresConnectionFactory(opts, NullLoggerFactory.Instance);
        var executor = factory.CreateExecutor();
        var result = await executor.ExecuteScalarAsync<int>("SELECT 1");
        Assert.Equal(1, result);
        await factory.DisposeAsync();
    }
}
