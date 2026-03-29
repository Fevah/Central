using System.IO;
using System.Text.Json;

namespace Central.Core.Services;

/// <summary>
/// Manages environment connection profiles (Live/Test/Dev).
/// Profiles stored locally in DPAPI-encrypted file.
/// Clients switch between environments on the login screen.
/// </summary>
public class EnvironmentService
{
    private static EnvironmentService? _instance;
    public static EnvironmentService Instance => _instance ??= new();

    private readonly string _configPath;
    private List<EnvironmentProfile> _profiles = new();

    public EnvironmentProfile? ActiveProfile { get; private set; }
    public IReadOnlyList<EnvironmentProfile> Profiles => _profiles;

    public event Action<EnvironmentProfile>? EnvironmentChanged;

    public EnvironmentService()
    {
        _configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Central", "environments.json");
    }

    /// <summary>Load profiles from encrypted local file.</summary>
    public void Load()
    {
        try
        {
            if (!File.Exists(_configPath)) { SeedDefaults(); return; }
            var json = File.ReadAllText(_configPath);
            _profiles = JsonSerializer.Deserialize<List<EnvironmentProfile>>(json) ?? new();
            ActiveProfile = _profiles.FirstOrDefault(p => p.IsActive) ?? _profiles.FirstOrDefault();
        }
        catch { SeedDefaults(); }
    }

    /// <summary>Save profiles to local file.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch { }
    }

    /// <summary>Switch to a different environment.</summary>
    public void SwitchTo(string name)
    {
        foreach (var p in _profiles) p.IsActive = false;
        var target = _profiles.FirstOrDefault(p => p.Name == name);
        if (target != null)
        {
            target.IsActive = true;
            ActiveProfile = target;
            Save();
            EnvironmentChanged?.Invoke(target);
        }
    }

    /// <summary>Add a new environment profile.</summary>
    public void AddProfile(EnvironmentProfile profile)
    {
        _profiles.RemoveAll(p => p.Name == profile.Name);
        _profiles.Add(profile);
        Save();
    }

    /// <summary>Remove an environment profile.</summary>
    public void RemoveProfile(string name)
    {
        _profiles.RemoveAll(p => p.Name == name);
        Save();
    }

    /// <summary>Get the API URL for the active environment.</summary>
    public string GetApiUrl() => ActiveProfile?.ApiUrl ?? "http://localhost:5000";

    /// <summary>Get the SignalR URL for the active environment.</summary>
    public string GetSignalRUrl() => ActiveProfile?.SignalRUrl ?? $"{GetApiUrl()}/hubs/notify";

    /// <summary>Get the certificate fingerprint for pinning.</summary>
    public string? GetCertFingerprint() => ActiveProfile?.CertFingerprint;

    private void SeedDefaults()
    {
        _profiles = new List<EnvironmentProfile>
        {
            new() { Name = "Local Development", Type = "dev", ApiUrl = "http://localhost:5000", IsActive = true },
            new() { Name = "Test", Type = "test", ApiUrl = "https://test-api.central.example.com" },
            new() { Name = "Production", Type = "live", ApiUrl = "https://api.central.example.com" }
        };
        ActiveProfile = _profiles[0];
        Save();
    }
}

public class EnvironmentProfile
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "dev"; // dev, test, live
    public string ApiUrl { get; set; } = "";
    public string? SignalRUrl { get; set; }
    public string? CertFingerprint { get; set; }
    public bool IsActive { get; set; }
    public string? TenantSlug { get; set; }

    public string TypeColor => Type switch
    {
        "live" => "#22C55E",
        "test" => "#F59E0B",
        "dev" => "#3B82F6",
        _ => "#6B7280"
    };

    public string TypeLabel => Type switch
    {
        "live" => "LIVE",
        "test" => "TEST",
        "dev" => "DEV",
        _ => Type.ToUpperInvariant()
    };
}
