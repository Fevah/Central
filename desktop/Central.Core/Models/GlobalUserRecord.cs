using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class GlobalUserRecord : INotifyPropertyChanged
{
    private Guid _id;
    private string _email = "";
    private string? _displayName;
    private bool _emailVerified;
    private bool _isGlobalAdmin;
    private DateTime _createdAt;
    private int _tenantCount;
    private string? _tenantSlugs;

    public Guid Id { get => _id; set { _id = value; N(); } }
    public string Email { get => _email; set { _email = value; N(); } }
    public string? DisplayName { get => _displayName; set { _displayName = value; N(); } }
    public bool EmailVerified { get => _emailVerified; set { _emailVerified = value; N(); N(nameof(VerifiedIcon)); } }
    public bool IsGlobalAdmin { get => _isGlobalAdmin; set { _isGlobalAdmin = value; N(); N(nameof(AdminIcon)); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    // Computed from JOIN — read-only display
    public int TenantCount { get => _tenantCount; set { _tenantCount = value; N(); } }
    public string? TenantSlugs { get => _tenantSlugs; set { _tenantSlugs = value; N(); } }

    // UI helpers
    public string VerifiedIcon => EmailVerified ? "\u2705" : "\u274C";
    public string AdminIcon => IsGlobalAdmin ? "\uD83D\uDD11" : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
