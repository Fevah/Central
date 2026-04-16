using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class TenantRecord : INotifyPropertyChanged
{
    private Guid _id;
    private string _slug = "";
    private string _displayName = "";
    private string? _domain;
    private string _tier = "free";
    private bool _isActive = true;
    private DateTime _createdAt;
    private DateTime _updatedAt;
    private int _userCount;
    private string? _planName;

    public Guid Id { get => _id; set { _id = value; N(); } }
    public string Slug { get => _slug; set { _slug = value; N(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; N(); } }
    public string? Domain { get => _domain; set { _domain = value; N(); } }
    public string Tier { get => _tier; set { _tier = value; N(); N(nameof(TierIcon)); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); N(nameof(StatusIcon)); N(nameof(StatusColor)); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    // Computed from JOIN — read-only display
    public int UserCount { get => _userCount; set { _userCount = value; N(); } }
    public string? PlanName { get => _planName; set { _planName = value; N(); } }

    // UI helpers
    public string StatusIcon => IsActive ? "\u2705" : "\u26D4";
    public string StatusColor => IsActive ? "#22C55E" : "#EF4444";
    public string TierIcon => Tier switch
    {
        "enterprise" => "\u2B50",
        "professional" => "\u2B50",
        _ => ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
