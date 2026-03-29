using Npgsql;

namespace Central.Api.Services;

/// <summary>
/// Retry helper for transient DB connection failures.
/// Retries up to 3 times with exponential backoff (100ms, 200ms, 400ms).
/// </summary>
public static class RetryHelper
{
    public static async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (NpgsqlException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(100 * (1 << attempt)); // 100, 200, 400ms
            }
        }
    }

    public static async Task WithRetryAsync(Func<Task> action, int maxRetries = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (NpgsqlException) when (attempt < maxRetries - 1)
            {
                await Task.Delay(100 * (1 << attempt));
            }
        }
    }
}
