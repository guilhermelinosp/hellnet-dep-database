namespace Hellnet.Database.Abstractions;

/// <summary>Generic repository. Not a DDD aggregate — just data access.</summary>
public interface IRepository<T>
    where T : class
{
    Task<T?> GetByIdAsync<TId>(TId id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> FindAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<PageResult<T>> PaginateAsync(ISpecification<T> spec, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
}

/// <summary>Query specification. Lean — no complex expression trees.</summary>
public interface ISpecification<T>
    where T : class
{
    string Sql { get; }
    object? Parameters { get; }
    string? OrderBy { get; }
}
