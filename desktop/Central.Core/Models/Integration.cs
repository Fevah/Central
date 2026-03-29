using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

/// <summary>Integration configuration — ManageEngine, ServiceNow, etc.</summary>
public class Integration : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _displayName = "";
    private string _integrationType = "oauth2";
    private string _baseUrl = "";
    private bool _isEnabled;
    private string _configJson = "{}";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; N(); } }
    public string IntegrationType { get => _integrationType; set { _integrationType = value; N(); } }
    public string BaseUrl { get => _baseUrl; set { _baseUrl = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); N(nameof(StatusIcon)); } }
    public string ConfigJson { get => _configJson; set { _configJson = value; N(); } }

    public string StatusIcon => IsEnabled ? "✅" : "⛔";
    public string StatusText => IsEnabled ? "Enabled" : "Disabled";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Encrypted credential for an integration.</summary>
public class IntegrationCredential
{
    public int Id { get; set; }
    public int IntegrationId { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";  // encrypted
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Integration sync log entry.</summary>
public class IntegrationLogEntry
{
    public int Id { get; set; }
    public int IntegrationId { get; set; }
    public string Action { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Message { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
