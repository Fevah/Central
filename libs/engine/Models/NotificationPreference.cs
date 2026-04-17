using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class NotificationPreference : INotifyPropertyChanged
{
    private int _id;
    private int _userId;
    private string _eventType = "";
    private string _channel = "toast";
    private bool _isEnabled = true;

    public int Id { get => _id; set { _id = value; N(); } }
    public int UserId { get => _userId; set { _userId = value; N(); } }
    public string EventType { get => _eventType; set { _eventType = value; N(); } }
    public string Channel { get => _channel; set { _channel = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); } }

    /// <summary>Human-readable event description.</summary>
    public string EventDescription => EventType switch
    {
        "sync_failure" => "Sync failure alerts",
        "sync_complete" => "Sync completion",
        "auth_lockout" => "Account lockout alerts",
        "backup_complete" => "Backup completion",
        "backup_failure" => "Backup failure alerts",
        "data_changed" => "Data changed by another user",
        "password_expiry" => "Password expiry warnings",
        "webhook_received" => "Webhook received",
        _ => EventType
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ActiveSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string SessionToken { get; set; } = "";
    public string AuthMethod { get; set; } = "";
    public string? IpAddress { get; set; }
    public string? MachineName { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }

    // Display
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string Duration => (DateTime.UtcNow - StartedAt).ToString(@"d\.hh\:mm");
    public string StatusColor => IsActive ? "#22C55E" : "#6B7280";
}

/// <summary>All supported notification event types.</summary>
public static class NotificationEventTypes
{
    public static readonly string[] All =
    [
        "sync_failure", "sync_complete",
        "auth_lockout", "backup_complete", "backup_failure",
        "data_changed", "password_expiry", "webhook_received"
    ];
}
