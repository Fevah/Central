namespace Central.Core.Services;

/// <summary>
/// Module settings framework. Modules declare settings via RegisterSettings(),
/// the engine provides persistence (DB) and UI (backstage/settings panel).
///
/// Usage in a module:
///   settings.Register("ping.timeout", "Ping Timeout (ms)", 5000, SettingType.Integer);
///   settings.Register("ssh.default_port", "Default SSH Port", 22, SettingType.Integer);
///   var timeout = settings.Get<int>("ping.timeout");
/// </summary>
public interface ISettingsProvider
{
    /// <summary>Register a setting with a default value.</summary>
    void Register(string key, string displayName, object defaultValue, SettingType type = SettingType.String, string? category = null, string? description = null);

    /// <summary>Get a setting value. Returns default if not set.</summary>
    T Get<T>(string key);

    /// <summary>Set a setting value. Persists immediately.</summary>
    Task SetAsync(string key, object value);

    /// <summary>Get all registered settings for UI rendering.</summary>
    IReadOnlyList<SettingDefinition> GetDefinitions();

    /// <summary>Get definitions for a specific category.</summary>
    IReadOnlyList<SettingDefinition> GetDefinitions(string category);
}

public enum SettingType { String, Integer, Boolean, Decimal, Choice }

public class SettingDefinition
{
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Description { get; init; } = "";
    public string Category { get; init; } = "General";
    public SettingType Type { get; init; } = SettingType.String;
    public object DefaultValue { get; init; } = "";
    public object CurrentValue { get; set; } = "";
    public string[]? Choices { get; init; }  // For SettingType.Choice
}
