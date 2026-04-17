using Central.Core.Models;

namespace Central.Core.Services;

/// <summary>
/// Engine-level icon resolution service. Caches admin defaults + user overrides in memory.
/// Call LoadAsync() once at startup, then Resolve() is O(1) dictionary lookup.
///
/// Resolution order: user override → admin default → code fallback (returns null).
///
/// Usage:
///   await IconOverrideService.Instance.LoadAsync(repo, userId);
///   var color = IconOverrideService.Instance.ResolveColor("status.device", "Active") ?? "#22C55E";
///   var iconName = IconOverrideService.Instance.ResolveIconName("device_type", "Core Switch");
/// </summary>
public class IconOverrideService
{
    private static IconOverrideService? _instance;
    public static IconOverrideService Instance => _instance ??= new();

    private readonly Dictionary<string, IconOverride> _adminDefaults = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IconOverride> _userOverrides = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;

    public bool IsLoaded => _loaded;

    /// <summary>Load all admin defaults + user overrides into memory. Call once at startup.</summary>
    public void Load(List<IconOverride> adminDefaults, List<IconOverride> userOverrides)
    {
        _adminDefaults.Clear();
        _userOverrides.Clear();

        foreach (var d in adminDefaults)
            _adminDefaults[$"{d.Context}:{d.ElementKey}"] = d;
        foreach (var u in userOverrides)
            _userOverrides[$"{u.Context}:{u.ElementKey}"] = u;

        _loaded = true;
    }

    /// <summary>Resolve an icon override. Returns null if no override exists (use code fallback).</summary>
    public IconOverride? Resolve(string context, string elementKey)
    {
        var key = $"{context}:{elementKey}";
        if (_userOverrides.TryGetValue(key, out var userOv)) return userOv;
        if (_adminDefaults.TryGetValue(key, out var adminOv)) return adminOv;
        return null;
    }

    /// <summary>Resolve just the colour. Returns null if no override.</summary>
    public string? ResolveColor(string context, string elementKey) =>
        Resolve(context, elementKey)?.Color;

    /// <summary>Resolve just the icon name. Returns null if no override.</summary>
    public string? ResolveIconName(string context, string elementKey) =>
        Resolve(context, elementKey)?.IconName;

    /// <summary>Resolve colour with a code fallback.</summary>
    public string ResolveColorOrDefault(string context, string elementKey, string fallback) =>
        ResolveColor(context, elementKey) ?? fallback;
}
