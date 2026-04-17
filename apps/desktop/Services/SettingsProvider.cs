using Central.Engine.Services;
using Central.Persistence;

namespace Central.Desktop.Services;

/// <summary>
/// DB-backed ISettingsProvider. Module settings are stored in user_settings
/// with a "mod." prefix to distinguish from layout/pref settings.
/// Falls back to default values when DB is unavailable.
/// </summary>
public class SettingsProvider : ISettingsProvider
{
    private readonly DbRepository _repo;
    private readonly int _userId;
    private readonly Dictionary<string, SettingDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SettingsProvider(DbRepository repo, int userId)
    {
        _repo = repo;
        _userId = userId;
    }

    public void Register(string key, string displayName, object defaultValue, SettingType type = SettingType.String, string? category = null, string? description = null)
    {
        var def = new SettingDefinition
        {
            Key = key,
            DisplayName = displayName,
            Description = description ?? "",
            Category = category ?? "General",
            Type = type,
            DefaultValue = defaultValue,
            CurrentValue = defaultValue
        };
        _definitions[key] = def;
        _cache[key] = defaultValue;
    }

    public T Get<T>(string key)
    {
        if (_cache.TryGetValue(key, out var val))
        {
            try { return (T)Convert.ChangeType(val, typeof(T)); }
            catch { }
        }
        if (_definitions.TryGetValue(key, out var def))
        {
            try { return (T)Convert.ChangeType(def.DefaultValue, typeof(T)); }
            catch { }
        }
        return default!;
    }

    public async Task SetAsync(string key, object value)
    {
        _cache[key] = value;
        if (_definitions.TryGetValue(key, out var def))
            def.CurrentValue = value;

        try
        {
            await _repo.SaveUserSettingAsync(_userId, $"mod.{key}", value?.ToString() ?? "");
        }
        catch { /* offline — cached value still used */ }
    }

    /// <summary>Load all registered settings from DB. Call once after all modules have registered.</summary>
    public async Task LoadFromDbAsync()
    {
        foreach (var def in _definitions.Values)
        {
            try
            {
                var val = await _repo.GetUserSettingAsync(_userId, $"mod.{def.Key}");
                if (val != null)
                {
                    def.CurrentValue = ConvertValue(val, def.Type, def.DefaultValue);
                    _cache[def.Key] = def.CurrentValue;
                }
            }
            catch { /* use default */ }
        }
    }

    public IReadOnlyList<SettingDefinition> GetDefinitions()
        => _definitions.Values.OrderBy(d => d.Category).ThenBy(d => d.DisplayName).ToList();

    public IReadOnlyList<SettingDefinition> GetDefinitions(string category)
        => _definitions.Values.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.DisplayName).ToList();

    private static object ConvertValue(string raw, SettingType type, object fallback)
    {
        try
        {
            return type switch
            {
                SettingType.Integer => int.Parse(raw),
                SettingType.Boolean => bool.Parse(raw),
                SettingType.Decimal => decimal.Parse(raw),
                _ => raw
            };
        }
        catch { return fallback; }
    }
}
