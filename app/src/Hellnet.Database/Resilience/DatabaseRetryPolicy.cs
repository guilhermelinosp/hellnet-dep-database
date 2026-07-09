using System.Diagnostics.CodeAnalysis;

using Hellnet.Database.Configuration;

using Microsoft.Extensions.Logging;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Hellnet.Database.Resilience;

/// <summary>
/// Retry policy using Polly.
/// Does NOT retry: syntax errors, constraint violations, permission denied.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class DatabaseRetryPolicy
{
    private readonly ResiliencePipeline _pipeline;
    private readonly ILogger<DatabaseRetryPolicy> _logger;

    public DatabaseRetryPolicy(HellnetDatabaseOptions options, ILogger<DatabaseRetryPolicy> logger)
    {
        _logger = logger;

        if (!options.RetryEnabled)
        {
            _pipeline = new ResiliencePipelineBuilder().Build();
            return;
        }

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.RetryMaxCount,
                DelayGenerator = static args =>
                    new ValueTask<TimeSpan?>(TimeSpan.FromMilliseconds(
                        Math.Pow(2, args.AttemptNumber + 1) * 100)),
                OnRetry = args =>
                {
                    _logger.LogWarning(args.Outcome.Exception,
                        "Transient error (attempt {Attempt}/{Max})",
                        args.AttemptNumber + 1, options.RetryMaxCount);
                    return default;
                },
                ShouldHandle = args =>
                {
                    if (args.Outcome.Exception is OperationCanceledException)
                    {
                        return ValueTask.FromResult(false);
                    }

                    if (args.Outcome.Exception is PostgresException pgEx)
                    {
                        return ValueTask.FromResult(pgEx.SqlState switch
                        {
                            "42601" => false, // syntax_error
                            "23505" => false, // unique_violation
                            "23503" => false, // foreign_key_violation
                            "42501" => false, // insufficient_privilege
                            "42P01" => false, // undefined_table
                            "42703" => false, // undefined_column
                            _ => true,
                        });
                    }
                    return ValueTask.FromResult(true);
                },
            })
            .Build();
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
        => await _pipeline.ExecuteAsync(async _ => await action(), ct);

    public async Task ExecuteAsync(Func<Task> action, CancellationToken ct = default)
        => await _pipeline.ExecuteAsync(async _ => await action(), ct);

    private static bool IsTransient(Outcome<object> outcome)
    {
        if (outcome.Exception is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                "42601" => false,
                "23505" => false,
                "23503" => false,
                "42501" => false,
                "42P01" => false,
                "42703" => false,
                _ => true,
            };
        }
        return outcome.Exception is not OperationCanceledException;
    }
}
