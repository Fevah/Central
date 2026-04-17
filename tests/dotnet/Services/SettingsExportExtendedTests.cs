using Central.Engine.Services;

namespace Central.Tests.Services;

public class SettingsExportExtendedTests
{
    // ── ExportedSettings defaults ──

    [Fact]
    public void ExportedSettings_Defaults()
    {
        var es = new SettingsExportService.ExportedSettings();
        Assert.Equal("", es.Username);
        Assert.Equal(0, es.UserId);
        Assert.Equal("1.0.0", es.AppVersion);
        Assert.NotNull(es.UserSettings);
        Assert.Empty(es.UserSettings);
        Assert.NotNull(es.NotificationPrefs);
        Assert.Empty(es.NotificationPrefs);
        Assert.NotNull(es.PanelCustomizations);
        Assert.Empty(es.PanelCustomizations);
        Assert.NotNull(es.SavedFilters);
        Assert.Empty(es.SavedFilters);
    }

    [Fact]
    public void ExportedSettings_ExportedAt_IsIsoFormat()
    {
        var es = new SettingsExportService.ExportedSettings();
        Assert.Contains("T", es.ExportedAt); // ISO 8601 format includes T
        Assert.True(DateTime.TryParse(es.ExportedAt, out _));
    }

    // ── Export with multiple settings ──

    [Fact]
    public async Task ExportAsync_WithMultipleSettings_ContainsAll()
    {
        var json = await SettingsExportService.ExportAsync(
            () => Task.FromResult(new Dictionary<string, string>
            {
                ["theme"] = "dark", ["language"] = "en", ["fontSize"] = "14"
            }),
            () => Task.FromResult(new List<Dictionary<string, object?>>
            {
                new() { ["event"] = "sync_failure", ["enabled"] = (object?)true },
                new() { ["event"] = "backup_complete", ["enabled"] = (object?)false }
            }),
            () => Task.FromResult(new List<Dictionary<string, object?>>
            {
                new() { ["panel"] = "IPAM", ["setting"] = (object?)"compact" }
            }),
            () => Task.FromResult(new List<Dictionary<string, object?>>
            {
                new() { ["name"] = "Active Only", ["filter"] = (object?)"status=Active" }
            }),
            "john.smith", 42);

        Assert.Contains("john.smith", json);
        Assert.Contains("42", json);
        Assert.Contains("theme", json);
        Assert.Contains("language", json);
        Assert.Contains("fontSize", json);
        Assert.Contains("sync_failure", json);
        Assert.Contains("backup_complete", json);
        Assert.Contains("IPAM", json);
        Assert.Contains("Active Only", json);
    }

    // ── Export with empty data ──

    [Fact]
    public async Task ExportAsync_WithEmptyData_ValidJson()
    {
        var json = await SettingsExportService.ExportAsync(
            () => Task.FromResult(new Dictionary<string, string>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            "", 0);

        Assert.Contains("ExportedAt", json);
        Assert.Contains("Username", json);
        Assert.Contains("UserId", json);
    }

    // ── Round-trip export/import via temp file ──

    [Fact]
    public async Task ExportImport_RoundTrip_PreservesAllData()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_rt_{Guid.NewGuid():N}.json");
        try
        {
            await SettingsExportService.ExportToFileAsync(path,
                () => Task.FromResult(new Dictionary<string, string> { ["key1"] = "val1" }),
                () => Task.FromResult(new List<Dictionary<string, object?>> { new() { ["a"] = (object?)"b" } }),
                () => Task.FromResult(new List<Dictionary<string, object?>>()),
                () => Task.FromResult(new List<Dictionary<string, object?>>()),
                "admin", 99);

            var imported = SettingsExportService.ImportFromFile(path);

            Assert.NotNull(imported);
            Assert.Equal("admin", imported!.Username);
            Assert.Equal(99, imported.UserId);
            Assert.Equal("1.0.0", imported.AppVersion);
            Assert.Single(imported.UserSettings);
            Assert.Equal("val1", imported.UserSettings["key1"]);
            Assert.Single(imported.NotificationPrefs);
            Assert.Empty(imported.PanelCustomizations);
            Assert.Empty(imported.SavedFilters);
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }

    // ── Export includes AppVersion ──

    [Fact]
    public async Task ExportAsync_IncludesAppVersion()
    {
        var json = await SettingsExportService.ExportAsync(
            () => Task.FromResult(new Dictionary<string, string>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            "test", 1);

        Assert.Contains("1.0.0", json);
    }

    // ── Import with malformed JSON ──

    [Fact]
    public void ImportFromFile_MalformedJson_Throws()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_bad_{Guid.NewGuid():N}.json");
        try
        {
            System.IO.File.WriteAllText(path, "{ invalid json ]]]");
            Assert.ThrowsAny<Exception>(() => SettingsExportService.ImportFromFile(path));
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
}
