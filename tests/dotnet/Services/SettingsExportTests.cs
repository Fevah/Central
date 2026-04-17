using Central.Core.Services;

namespace Central.Tests.Services;

public class SettingsExportTests
{
    [Fact]
    public async Task ExportAsync_ReturnsValidJson()
    {
        var json = await SettingsExportService.ExportAsync(
            () => Task.FromResult(new Dictionary<string, string> { ["theme"] = "dark" }),
            () => Task.FromResult(new List<Dictionary<string, object?>> { new() { ["event"] = "sync" } }),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            () => Task.FromResult(new List<Dictionary<string, object?>>()),
            "testuser", 1);

        Assert.Contains("testuser", json);
        Assert.Contains("theme", json);
        Assert.Contains("dark", json);
        Assert.Contains("ExportedAt", json);
    }

    [Fact]
    public void ImportFromFile_NonExistent_Throws()
    {
        Assert.ThrowsAny<Exception>(() =>
            SettingsExportService.ImportFromFile("/nonexistent/path/file_" + Guid.NewGuid() + ".json"));
    }

    [Fact]
    public async Task ExportToFile_CreatesFile()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"test_export_{Guid.NewGuid():N}.json");
        try
        {
            await SettingsExportService.ExportToFileAsync(path,
                () => Task.FromResult(new Dictionary<string, string>()),
                () => Task.FromResult(new List<Dictionary<string, object?>>()),
                () => Task.FromResult(new List<Dictionary<string, object?>>()),
                () => Task.FromResult(new List<Dictionary<string, object?>>()),
                "test", 1);

            Assert.True(System.IO.File.Exists(path));
            var content = await System.IO.File.ReadAllTextAsync(path);
            Assert.Contains("test", content);

            // Round-trip
            var imported = SettingsExportService.ImportFromFile(path);
            Assert.NotNull(imported);
            Assert.Equal("test", imported!.Username);
        }
        finally
        {
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
        }
    }
}
