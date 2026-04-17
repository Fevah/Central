namespace Central.Engine.Auth;

/// <summary>Audit logger for authentication events.</summary>
public interface IAuthEventLogger
{
    Task LogAsync(string eventType, string? username, bool success,
        string? providerType = null, int? userId = null,
        string? errorMessage = null, Dictionary<string, object?>? metadata = null);
}
