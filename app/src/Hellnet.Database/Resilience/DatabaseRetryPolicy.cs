using Hellnet.Database.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Hellnet.Database.Resilience;

/// <summary>
/// Retry policy with error discrimination.
/// Does NOT retry: syntax errors, constraint violations, permission denied.
/// </summary>
internal sealed class DatabaseRetryPolicy
{
    private readonly HellnetDatabaseOptions _options;
    private readonly ILogger<DatabaseRetryPolicy> _logger;

    public DatabaseRetryPolicy(HellnetDatabaseOptions options, ILogger<DatabaseRetryPolicy> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct = default)
    {
        if (!_options.RetryEnabled)
            return await action();

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await action();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (IsPermanent(ex))
            {
                _logger.LogError(ex, "Permanent error, not retrying");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Transient error (attempt {Attempt}/{Max})", attempt, _options.RetryMaxCount);
                if (attempt >= _options.RetryMaxCount)
                    throw;

                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * _options.RetryBaseDelayMs);
                await Task.Delay(delay, ct);
            }
        }
    }

    private static bool IsPermanent(Exception ex)
    {
        if (ex is PostgresException pgEx)
        {
            return pgEx.SqlState switch
            {
                "42601" => true, // syntax_error
                "23505" => true, // unique_violation
                "23503" => true, // foreign_key_violation
                "42501" => true, // insufficient_privilege
                "42P01" => true, // undefined_table
                "42703" => true, // undefined_column
                _ => false,
            };
        }
        return false;
    }
}
