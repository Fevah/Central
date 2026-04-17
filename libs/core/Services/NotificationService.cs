using System.Collections.ObjectModel;

namespace Central.Core.Services;

/// <summary>
/// Engine notification service. Modules publish notifications;
/// the shell renders them as toasts in the status bar area.
/// </summary>
public class NotificationService
{
    private static NotificationService? _instance;
    public static NotificationService Instance => _instance ??= new();

    public ObservableCollection<Notification> Recent { get; } = new();

    /// <summary>Fires when a new notification arrives. Shell wires this to show toast.</summary>
    public event Action<Notification>? NotificationReceived;

    public void Info(string title, string message = "", string? source = null)
        => Push(new Notification(NotificationType.Info, title, message, source));

    public void Success(string title, string message = "", string? source = null)
        => Push(new Notification(NotificationType.Success, title, message, source));

    public void Warning(string title, string message = "", string? source = null)
        => Push(new Notification(NotificationType.Warning, title, message, source));

    public void Error(string title, string message = "", string? source = null)
        => Push(new Notification(NotificationType.Error, title, message, source));

    /// <summary>
    /// Send an event-type notification that respects user preferences.
    /// Checks the cached preferences to determine toast vs email vs none.
    /// </summary>
    public void NotifyEvent(string eventType, string title, string message = "", NotificationType type = NotificationType.Info)
    {
        // Check preferences (if loaded)
        if (_userPreferences.TryGetValue(eventType, out var pref))
        {
            if (!pref.IsEnabled || pref.Channel == "none") return;

            // Toast (default)
            if (pref.Channel is "toast" or "both")
                Push(new Notification(type, title, message, eventType));

            // Email (async, fire-and-forget)
            if (pref.Channel is "email" or "both")
                EmailRequested?.Invoke(eventType, title, message);
        }
        else
        {
            // No preference set — default to toast
            Push(new Notification(type, title, message, eventType));
        }
    }

    /// <summary>Fired when a notification should be sent via email. Wire to EmailService.</summary>
    public event Action<string, string, string>? EmailRequested;

    /// <summary>Cache user preferences at login. Call once after auth.</summary>
    public void LoadPreferences(IEnumerable<Models.NotificationPreference> prefs)
    {
        _userPreferences.Clear();
        foreach (var p in prefs)
            _userPreferences[p.EventType] = p;
    }

    private readonly Dictionary<string, Models.NotificationPreference> _userPreferences = new();

    /// <summary>Clear all recent notifications.</summary>
    public void ClearRecent() => Recent.Clear();

    private void Push(Notification n)
    {
        // Keep last 50
        if (Recent.Count >= 50) Recent.RemoveAt(Recent.Count - 1);
        Recent.Insert(0, n);
        NotificationReceived?.Invoke(n);
    }
}

public enum NotificationType { Info, Success, Warning, Error }

public class Notification
{
    public NotificationType Type { get; }
    public string Title { get; }
    public string Message { get; }
    public string? Source { get; }
    public DateTime Timestamp { get; } = DateTime.Now;
    public string Icon => Type switch
    {
        NotificationType.Success => "\u2713",  // ✓
        NotificationType.Warning => "\u26A0",  // ⚠
        NotificationType.Error   => "\u2717",  // ✗
        _ => "\u2139"                           // ℹ
    };
    public string Color => Type switch
    {
        NotificationType.Success => "#22C55E",
        NotificationType.Warning => "#F59E0B",
        NotificationType.Error   => "#EF4444",
        _ => "#3B82F6"
    };

    public Notification(NotificationType type, string title, string message, string? source)
    {
        Type = type; Title = title; Message = message; Source = source;
    }
}
