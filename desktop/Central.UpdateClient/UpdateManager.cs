using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace Central.UpdateClient;

/// <summary>
/// Client-side update manager — checks for updates, downloads packages,
/// applies updates, restarts the application.
/// Supports: full packages and delta updates.
/// </summary>
public class UpdateManager
{
    private readonly HttpClient _http;
    private readonly string _updateServerUrl;
    private readonly string _currentVersion;
    private readonly string _platform;
    private readonly string _appDir;
    private readonly string _backupDir;

    public event Action<string>? StatusChanged;
    public event Action<int>? ProgressChanged;

    public UpdateManager(string updateServerUrl, string currentVersion, string platform = "windows-x64")
    {
        _updateServerUrl = updateServerUrl.TrimEnd('/');
        _currentVersion = currentVersion;
        _platform = platform;
        _http = new HttpClient();
        _appDir = AppDomain.CurrentDomain.BaseDirectory;
        _backupDir = Path.Combine(_appDir, ".update-backup");
    }

    /// <summary>Check if an update is available.</summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var url = $"{_updateServerUrl}/api/updates/check?current={_currentVersion}&platform={_platform}";
            var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            var info = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (info == null || info.Version == _currentVersion) return null;
            return info;
        }
        catch { return null; }
    }

    /// <summary>Download and apply an update.</summary>
    public async Task<UpdateResult> ApplyUpdateAsync(UpdateInfo update)
    {
        try
        {
            StatusChanged?.Invoke($"Downloading v{update.Version}...");

            // Download package
            var packagePath = Path.Combine(Path.GetTempPath(), $"central-update-{update.Version}.zip");
            await DownloadWithProgressAsync(update.PackageUrl, packagePath);

            // Verify checksum
            if (!string.IsNullOrEmpty(update.Checksum))
            {
                StatusChanged?.Invoke("Verifying integrity...");
                var actualHash = ComputeFileHash(packagePath);
                if (actualHash != update.Checksum)
                {
                    File.Delete(packagePath);
                    return UpdateResult.Fail("Checksum mismatch — download corrupted");
                }
            }

            // Backup current version
            StatusChanged?.Invoke("Backing up current version...");
            BackupCurrentVersion();

            // Extract update
            StatusChanged?.Invoke("Applying update...");
            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, _appDir, overwriteFiles: true);
            }
            catch (Exception ex)
            {
                // Rollback on failure
                StatusChanged?.Invoke("Update failed — rolling back...");
                Rollback();
                return UpdateResult.Fail($"Extract failed: {ex.Message}. Rolled back to previous version.");
            }

            // Cleanup
            File.Delete(packagePath);
            StatusChanged?.Invoke($"Update to v{update.Version} complete. Restart to apply.");

            // Report success to server
            try
            {
                await _http.PostAsJsonAsync($"{_updateServerUrl}/api/updates/report", new
                {
                    version = update.Version,
                    platform = _platform,
                    status = "success"
                });
            }
            catch { }

            return UpdateResult.Ok(update.Version);
        }
        catch (Exception ex)
        {
            return UpdateResult.Fail(ex.Message);
        }
    }

    /// <summary>Rollback to the backed-up version.</summary>
    public bool Rollback()
    {
        try
        {
            if (!Directory.Exists(_backupDir)) return false;

            foreach (var file in Directory.GetFiles(_backupDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(_backupDir, file);
                var targetPath = Path.Combine(_appDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(file, targetPath, overwrite: true);
            }

            Directory.Delete(_backupDir, recursive: true);
            StatusChanged?.Invoke("Rollback complete");
            return true;
        }
        catch { return false; }
    }

    /// <summary>Restart the application after update.</summary>
    public void RestartApplication()
    {
        var exePath = Path.Combine(_appDir, "Central.exe");
        if (File.Exists(exePath))
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
            Environment.Exit(0);
        }
    }

    private void BackupCurrentVersion()
    {
        if (Directory.Exists(_backupDir))
            Directory.Delete(_backupDir, recursive: true);
        Directory.CreateDirectory(_backupDir);

        foreach (var file in Directory.GetFiles(_appDir, "Central*.dll"))
        {
            var dest = Path.Combine(_backupDir, Path.GetFileName(file));
            File.Copy(file, dest);
        }

        var exeSrc = Path.Combine(_appDir, "Central.exe");
        if (File.Exists(exeSrc))
            File.Copy(exeSrc, Path.Combine(_backupDir, "Central.exe"));
    }

    private async Task DownloadWithProgressAsync(string url, string outputPath)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var totalBytes = resp.Content.Headers.ContentLength ?? -1;
        await using var source = await resp.Content.ReadAsStreamAsync();
        await using var dest = File.Create(outputPath);

        var buffer = new byte[81920];
        long downloaded = 0;
        int read;

        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read));
            downloaded += read;
            if (totalBytes > 0)
                ProgressChanged?.Invoke((int)(downloaded * 100 / totalBytes));
        }
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToBase64String(SHA256.HashData(stream));
    }
}

public class UpdateInfo
{
    public string Version { get; set; } = "";
    public string PackageUrl { get; set; } = "";
    public string? Checksum { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool IsMandatory { get; set; }
    public string? DeltaFrom { get; set; }
}

public class UpdateResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NewVersion { get; set; }
    public bool RequiresRestart { get; set; } = true;

    public static UpdateResult Ok(string version) => new() { Success = true, NewVersion = version };
    public static UpdateResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}
