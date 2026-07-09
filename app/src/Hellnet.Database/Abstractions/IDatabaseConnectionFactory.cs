namespace Hellnet.Database.Abstractions;

/// <summary>Creates and manages database connections. Provider-agnostic.</summary>
public interface IDatabaseConnectionFactory
{
    IDatabaseExecutor CreateExecutor();
    IDatabaseTransaction CreateTransaction();
}
