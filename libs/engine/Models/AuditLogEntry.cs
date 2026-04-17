using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class AuditLogEntry : INotifyPropertyChanged
{
    public int Id { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorEmail { get; set; } = "";
    public string Action { get; set; } = "";
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }

    // UI helpers
    public string ActionIcon => Action switch
    {
        "tenant_created" or "user_invited" or "module_granted" => "+",
        "tenant_deleted" or "user_removed" or "module_revoked" => "-",
        "tenant_suspended" or "subscription_cancelled" => "\u26D4",
        "tenant_activated" => "\u2705",
        _ => "\u2022"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
