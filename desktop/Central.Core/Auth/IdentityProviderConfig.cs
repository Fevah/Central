using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Auth;

/// <summary>DB-backed IdP configuration record.</summary>
public class IdentityProviderConfig : INotifyPropertyChanged
{
    private int _id;
    private string _providerType = "";
    private string _name = "";
    private bool _isEnabled = true;
    private bool _isDefault;
    private int _priority = 100;
    private string _configJson = "{}";
    private string? _metadataUrl;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string ProviderType { get => _providerType; set { _providerType = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }
    public bool IsDefault { get => _isDefault; set { _isDefault = value; OnPropertyChanged(); } }
    public int Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
    public string ConfigJson { get => _configJson; set { _configJson = value; OnPropertyChanged(); } }
    public string? MetadataUrl { get => _metadataUrl; set { _metadataUrl = value; OnPropertyChanged(); } }

    /// <summary>Parsed config values. Populated by the service layer after decryption.</summary>
    public Dictionary<string, string> Config { get; set; } = new();

    public string GetConfigValue(string key, string defaultValue = "")
        => Config.TryGetValue(key, out var val) ? val : defaultValue;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Domain-to-provider mapping for IdP discovery.</summary>
public class IdpDomainMapping
{
    public int Id { get; set; }
    public string EmailDomain { get; set; } = "";
    public int ProviderId { get; set; }
    public string? ProviderName { get; set; }
}

/// <summary>Claims-to-role mapping rule.</summary>
public class ClaimMapping : INotifyPropertyChanged
{
    private int _id;
    private int? _providerId;
    private string _claimType = "";
    private string _claimValue = "";
    private string _targetRole = "";
    private int _priority = 100;
    private bool _isEnabled = true;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int? ProviderId { get => _providerId; set { _providerId = value; OnPropertyChanged(); } }
    public string ClaimType { get => _claimType; set { _claimType = value; OnPropertyChanged(); } }
    public string ClaimValue { get => _claimValue; set { _claimValue = value; OnPropertyChanged(); } }
    public string TargetRole { get => _targetRole; set { _targetRole = value; OnPropertyChanged(); } }
    public int Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>External identity link between a Central user and an IdP account.</summary>
public class UserExternalIdentity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProviderId { get; set; }
    public string ExternalId { get; set; } = "";
    public string? ExternalEmail { get; set; }
    public string? ExternalClaimsJson { get; set; }
    public DateTime? LinkedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? ProviderName { get; set; }
    public string? Username { get; set; }
}

/// <summary>Auth event audit log entry.</summary>
public class AuthEvent
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = "";
    public string? ProviderType { get; set; }
    public string? Username { get; set; }
    public int? UserId { get; set; }
    public string? IpAddress { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Social provider configuration.</summary>
public class SocialProviderConfig
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = "";
    public string ClientId { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string Scopes { get; set; } = "openid email profile";
}
