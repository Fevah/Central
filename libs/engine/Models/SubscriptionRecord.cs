using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class SubscriptionRecord : INotifyPropertyChanged
{
    private int _id;
    private Guid _tenantId;
    private string _tenantSlug = "";
    private string _tenantName = "";
    private string _tier = "";
    private string _planName = "";
    private int? _maxUsers;
    private int? _maxDevices;
    private string _status = "active";
    private DateTime _startedAt;
    private DateTime? _expiresAt;
    private string? _stripeSubId;

    public int Id { get => _id; set { _id = value; N(); } }
    public Guid TenantId { get => _tenantId; set { _tenantId = value; N(); } }
    public string TenantSlug { get => _tenantSlug; set { _tenantSlug = value; N(); } }
    public string TenantName { get => _tenantName; set { _tenantName = value; N(); } }
    public string Tier { get => _tier; set { _tier = value; N(); } }
    public string PlanName { get => _planName; set { _planName = value; N(); } }
    public int? MaxUsers { get => _maxUsers; set { _maxUsers = value; N(); N(nameof(MaxUsersDisplay)); } }
    public int? MaxDevices { get => _maxDevices; set { _maxDevices = value; N(); N(nameof(MaxDevicesDisplay)); } }
    public string Status { get => _status; set { _status = value; N(); N(nameof(StatusIcon)); N(nameof(StatusColor)); } }
    public DateTime StartedAt { get => _startedAt; set { _startedAt = value; N(); } }
    public DateTime? ExpiresAt { get => _expiresAt; set { _expiresAt = value; N(); N(nameof(IsExpired)); } }
    public string? StripeSubId { get => _stripeSubId; set { _stripeSubId = value; N(); } }

    // UI helpers
    public string MaxUsersDisplay => MaxUsers.HasValue ? MaxUsers.Value.ToString() : "Unlimited";
    public string MaxDevicesDisplay => MaxDevices.HasValue ? MaxDevices.Value.ToString() : "Unlimited";
    public string StatusIcon => Status switch { "active" => "\u2705", "trial" => "\u23F3", "cancelled" => "\u274C", _ => "\u2753" };
    public string StatusColor => Status switch { "active" => "#22C55E", "trial" => "#F59E0B", "cancelled" => "#EF4444", _ => "#6B7280" };
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
