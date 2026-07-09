namespace Hellnet.Database.Abstractions;

/// <summary>Creates and manages database connections. Provider-agnostic.</summary>
public interface IDatabaseConnectionFactory
{
    public IDatabaseExecutor CreateExecutor();
    public IDatabaseTransaction CreateTransaction();
}
