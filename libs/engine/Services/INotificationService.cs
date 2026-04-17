namespace Central.Engine.Services;

/// <summary>
/// Status bar / toast notification service.
/// </summary>
public interface INotificationService
{
    void Info(string message);
    void Success(string message);
    void Warn(string message);
    void Error(string message);
}
