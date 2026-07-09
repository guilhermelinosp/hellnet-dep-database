namespace Hellnet.Database.Abstractions;

/// <summary>Typed result wrapping success/failure with diagnostics.</summary>
public sealed record DatabaseResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    public static DatabaseResult<T> Success(T data, TimeSpan duration) => new()
    {
        IsSuccess = true, Data = data, Duration = duration,
    };

    public static DatabaseResult<T> Failure(string error, TimeSpan duration) => new()
    {
        IsSuccess = false, ErrorMessage = error, Duration = duration,
    };
}

/// <summary>Paginated result.</summary>
public sealed record PageResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasNextPage => (Page * PageSize) < TotalCount;
}
