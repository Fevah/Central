using System.Reflection;
using System.Security.Cryptography;

namespace Central.Protection;

/// <summary>
/// Runtime integrity verification — checks that loaded assemblies haven't been tampered with.
/// Computes SHA-256 of key DLLs and compares against a signed manifest.
/// Run at startup to detect binary modification.
/// </summary>
public static class IntegrityChecker
{
    /// <summary>Compute SHA-256 hash of an assembly file.</summary>
    public static string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Verify all assemblies in the current directory against a manifest.
    /// Manifest format: Dictionary of filename → expected SHA-256 hash.
    /// </summary>
    public static IntegrityResult VerifyAll(Dictionary<string, string> manifest)
    {
        var result = new IntegrityResult();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var (fileName, expectedHash) in manifest)
        {
            var filePath = Path.Combine(baseDir, fileName);
            if (!File.Exists(filePath))
            {
                result.MissingFiles.Add(fileName);
                continue;
            }

            var actualHash = ComputeHash(filePath);
            if (actualHash != expectedHash)
                result.TamperedFiles.Add(fileName);
            else
                result.VerifiedFiles.Add(fileName);
        }

        result.IsIntact = result.MissingFiles.Count == 0 && result.TamperedFiles.Count == 0;
        return result;
    }

    /// <summary>Generate a manifest for all DLLs in the current directory.</summary>
    public static Dictionary<string, string> GenerateManifest()
    {
        var manifest = new Dictionary<string, string>();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var dll in Directory.GetFiles(baseDir, "Central*.dll"))
        {
            manifest[Path.GetFileName(dll)] = ComputeHash(dll);
        }

        return manifest;
    }

    /// <summary>Verify the current executing assembly hasn't been modified.</summary>
    public static bool VerifySelf(string expectedHash)
    {
        var location = Assembly.GetExecutingAssembly().Location;
        if (string.IsNullOrEmpty(location)) return true; // Single-file publish — can't verify
        return ComputeHash(location) == expectedHash;
    }
}

public class IntegrityResult
{
    public bool IsIntact { get; set; }
    public List<string> VerifiedFiles { get; } = new();
    public List<string> TamperedFiles { get; } = new();
    public List<string> MissingFiles { get; } = new();

    public string Summary => IsIntact
        ? $"Integrity OK — {VerifiedFiles.Count} files verified"
        : $"INTEGRITY VIOLATION — {TamperedFiles.Count} tampered, {MissingFiles.Count} missing";
}
