using System.Security.Cryptography;
using System.Text;

namespace Central.Core.Auth;

/// <summary>
/// AES-256 encryption for SSH credentials stored in the database.
/// Key derived from a server-side secret (env var or config file).
/// Credentials are encrypted at rest, decrypted only when needed for SSH.
/// </summary>
public static class CredentialEncryptor
{
    private static byte[]? _key;

    /// <summary>Machine-scoped fallback key (not hardcoded — derived from machine name + user at runtime).</summary>
    private static byte[] FallbackKey => SHA256.HashData(
        Encoding.UTF8.GetBytes($"Central-{Environment.MachineName}-{Environment.UserName}"));

    /// <summary>Initialize with a secret key. Call once at startup. Reads CENTRAL_CREDENTIAL_KEY or CENTRAL_CREDENTIAL_KEY env var.</summary>
    public static void Initialize(string? secretKey = null)
    {
        if (string.IsNullOrEmpty(secretKey))
            secretKey = Environment.GetEnvironmentVariable("CENTRAL_CREDENTIAL_KEY")
                     ?? Environment.GetEnvironmentVariable("CENTRAL_CREDENTIAL_KEY");

        _key = string.IsNullOrEmpty(secretKey)
            ? FallbackKey
            : SHA256.HashData(Encoding.UTF8.GetBytes(secretKey));
    }

    /// <summary>Encrypt a credential string. Returns Base64.</summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var key = _key ?? FallbackKey;

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to ciphertext
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        aes.IV.CopyTo(result, 0);
        cipherBytes.CopyTo(result, aes.IV.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>Decrypt a credential string from Base64.</summary>
    public static string Decrypt(string cipherBase64)
    {
        if (string.IsNullOrEmpty(cipherBase64)) return "";

        // If it doesn't look like Base64, return as-is (legacy unencrypted value)
        try { Convert.FromBase64String(cipherBase64); }
        catch { return cipherBase64; }

        var key = _key ?? FallbackKey;
        var fullBytes = Convert.FromBase64String(cipherBase64);

        if (fullBytes.Length < 17) return cipherBase64; // Too short to be encrypted

        using var aes = Aes.Create();
        aes.Key = key;

        var iv = new byte[16];
        Array.Copy(fullBytes, 0, iv, 0, 16);
        aes.IV = iv;

        var cipherBytes = new byte[fullBytes.Length - 16];
        Array.Copy(fullBytes, 16, cipherBytes, 0, cipherBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>Check if a value appears to be encrypted (Base64 with IV prefix).</summary>
    public static bool IsEncrypted(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        try
        {
            var bytes = Convert.FromBase64String(value);
            return bytes.Length >= 17; // At least IV (16) + 1 byte ciphertext
        }
        catch { return false; }
    }
}
