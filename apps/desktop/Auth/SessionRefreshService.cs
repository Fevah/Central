using Central.Engine.Auth;

namespace Central.Desktop.Auth;

/// <summary>
/// Background timer that refreshes the Central session before token expiry.
/// Runs silently. On failure, shows a non-modal re-auth prompt.
/// </summary>
public class SessionRefreshService : IDisposable
{
    private readonly IAuthenticationService _authService;
    private System.Threading.Timer? _timer;
    private int _failCount;
    private static readonly int[] BackoffSeconds = [5, 15, 30, 60, 120, 300];

    public SessionRefreshService(IAuthenticationService authService)
    {
        _authService = authService;
    }

    /// <summary>Start the refresh timer. Fires at intervalMinutes.</summary>
    public void Start(int intervalMinutes = 20)
    {
        _timer?.Dispose();
        _timer = new System.Threading.Timer(async _ => await RefreshAsync(),
            null, TimeSpan.FromMinutes(intervalMinutes), TimeSpan.FromMinutes(intervalMinutes));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private async Task RefreshAsync()
    {
        try
        {
            var success = await _authService.TryRefreshSessionAsync();
            if (success)
            {
                _failCount = 0;
                return;
            }

            _failCount++;
            OnRefreshFailed();
        }
        catch
        {
            _failCount++;
            OnRefreshFailed();
        }
    }

    private void OnRefreshFailed()
    {
        var backoffIndex = Math.Min(_failCount - 1, BackoffSeconds.Length - 1);
        var backoffMs = BackoffSeconds[backoffIndex] * 1000;

        // Reschedule with backoff
        _timer?.Change(backoffMs, Timeout.Infinite);

        // Notify UI (via notification service if available)
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Central.Engine.Services.NotificationService.Instance?.Warning(
                    "Session refresh failed. Click your profile to re-authenticate.");
            });
        }
        catch { }
    }

    /// <summary>Raised when refresh fails repeatedly and user must re-authenticate.</summary>
    public event Action? ReauthRequired;

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
