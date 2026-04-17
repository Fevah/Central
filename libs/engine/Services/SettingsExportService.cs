using System.Text.Json;

namespace Central.Engine.Services;

/// <summary>
/// Export and import all user settings as a JSON file.
/// Includes: notification prefs, panel customizations, saved filters, ribbon overrides, layout prefs.
/// Useful for: backup, migration between machines, cloning user config.
/// </summary>
public static class SettingsExportService
{
    public class ExportedSettings
    {
        public string ExportedAt { get; set; } = DateTime.UtcNow.ToString("O");
        public string Username { get; set; } = "";
        public int UserId { get; set; }
        public string AppVersion { get; set; } = "1.0.0";
        public Dictionary<string, string> UserSettings { get; set; } = new();
        public List<Dictionary<string, object?>> NotificationPrefs { get; set; } = new();
        public List<Dictionary<string, object?>> PanelCustomizations { get; set; } = new();
        public List<Dictionary<string, object?>> SavedFilters { get; set; } = new();
    }

    /// <summary>Export all settings for a user to a JSON string.</summary>
    public static async Task<string> ExportAsync(Func<Task<Dictionary<string, string>>> getUserSettings,
        Func<Task<List<Dictionary<string, object?>>>> getNotifPrefs,
        Func<Task<List<Dictionary<string, object?>>>> getPanelCustom,
        Func<Task<List<Dictionary<string, object?>>>> getSavedFilters,
        string username, int userId)
    {
        var export = new ExportedSettings
        {
            Username = username,
            UserId = userId,
            UserSettings = await getUserSettings(),
            NotificationPrefs = await getNotifPrefs(),
            PanelCustomizations = await getPanelCustom(),
            SavedFilters = await getSavedFilters()
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Export to a file.</summary>
    public static async Task ExportToFileAsync(string filePath,
        Func<Task<Dictionary<string, string>>> getUserSettings,
        Func<Task<List<Dictionary<string, object?>>>> getNotifPrefs,
        Func<Task<List<Dictionary<string, object?>>>> getPanelCustom,
        Func<Task<List<Dictionary<string, object?>>>> getSavedFilters,
        string username, int userId)
    {
        var json = await ExportAsync(getUserSettings, getNotifPrefs, getPanelCustom, getSavedFilters, username, userId);
        await System.IO.File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>Parse an exported settings file.</summary>
    public static ExportedSettings? ImportFromFile(string filePath)
    {
        var json = System.IO.File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<ExportedSettings>(json);
    }
}
