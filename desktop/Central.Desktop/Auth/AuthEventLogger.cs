using Central.Core.Auth;
using Central.Data;

namespace Central.Desktop.Auth;

/// <summary>Logs authentication events to the auth_events table.</summary>
public class AuthEventLogger : IAuthEventLogger
{
    private readonly DbRepository _repo;

    public AuthEventLogger(DbRepository repo) => _repo = repo;

    public async Task LogAsync(string eventType, string? username, bool success,
        string? providerType = null, int? userId = null,
        string? errorMessage = null, Dictionary<string, object?>? metadata = null)
    {
        try
        {
            await _repo.LogAuthEventAsync(eventType, username, success, providerType, userId, errorMessage);
        }
        catch
        {
            // Auth logging must never block the auth flow
        }
    }
}
