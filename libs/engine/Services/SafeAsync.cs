namespace Central.Engine.Services;

/// <summary>
/// Safe async execution wrapper — catches exceptions from async void handlers
/// and routes them to the notification service + app logger instead of crashing.
///
/// Usage in event handlers:
///   button.Click += (s, e) => SafeAsync.Run(async () => await DoWork());
///
/// Instead of:
///   button.Click += async (s, e) => await DoWork(); // DANGEROUS — async void with no try/catch
/// </summary>
public static class SafeAsync
{
    /// <summary>Execute an async action safely — exceptions are logged, not thrown.</summary>
    public static async void Run(Func<Task> action, string? context = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var msg = context != null ? $"{context}: {ex.Message}" : ex.Message;
            System.Diagnostics.Debug.WriteLine($"[SafeAsync] {msg}");
            try { NotificationService.Instance?.Error($"Error: {msg}"); } catch { }
        }
    }

    /// <summary>Execute with CommandGuard protection + safe error handling.</summary>
    public static async void RunGuarded(string commandName, Func<Task> action, string? context = null)
    {
        if (!CommandGuard.TryEnter(commandName)) return;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            var msg = context != null ? $"{context}: {ex.Message}" : ex.Message;
            System.Diagnostics.Debug.WriteLine($"[SafeAsync] {msg}");
            try { NotificationService.Instance?.Error($"Error: {msg}"); } catch { }
        }
        finally
        {
            CommandGuard.Exit(commandName);
        }
    }
}
