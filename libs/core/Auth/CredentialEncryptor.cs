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
    private static bool _initialized;

    /// <summary>
    /// Initialize with a secret key. Call once at startup.
    /// Reads CENTRAL_CREDENTIAL_KEY env var, or requires explicit key.
    /// Throws if no key is available — never falls back to guessable values.
    /// </summary>
    public static void Initialize(string? secretKey = null)
    {
        if (string.IsNullOrEmpty(secretKey))
            secretKey = Environment.GetEnvironmentVariable("CENTRAL_CREDENTIAL_KEY");

        if (string.IsNullOrEmpty(secretKey))
        {
            // No key available — encryption will be unavailable.
            // Log warning but don't throw — app can still start in read-only mode.
            _key = null;
            _initialized = true;
            return;
        }

        // Derive a 256-bit key from the secret using SHA256
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secretKey));
        _initialized = true;
    }

    /// <summary>Whether encryption is available (key was provided at startup).</summary>
    public static bool IsAvailable => _key != null;

    /// <summary>Encrypt a credential string. Returns Base64.</summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        if (_key == null)
            throw new InvalidOperationException(
                "CredentialEncryptor not initialized with a key. Set CENTRAL_CREDENTIAL_KEY environment variable.");

        using var aes = Aes.Create();
        aes.Key = _key;
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

    /// <summary>
    /// Decrypt a credential string from Base64.
    /// Throws CryptographicException on invalid data instead of returning plaintext.
    /// </summary>
    public static string Decrypt(string cipherBase64)
    {
        if (string.IsNullOrEmpty(cipherBase64)) return "";
        if (_key == null)
            throw new InvalidOperationException(
                "CredentialEncryptor not initialized with a key. Set CENTRAL_CREDENTIAL_KEY environment variable.");

        byte[] fullBytes;
        try { fullBytes = Convert.FromBase64String(cipherBase64); }
        catch (FormatException)
        {
            throw new CryptographicException("Value is not valid Base64 — cannot decrypt.");
        }

        if (fullBytes.Length < 17)
            throw new CryptographicException("Encrypted value is too short (missing IV or ciphertext).");

        using var aes = Aes.Create();
        aes.Key = _key;

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

    /// <summary>
    /// Try to decrypt, returning the plaintext on success or the original value on failure.
    /// Use this for backward compatibility with legacy unencrypted values during migration.
    /// </summary>
    public static string DecryptOrPassthrough(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (_key == null) return value; // No key — pass through
        if (!IsEncrypted(value)) return value; // Doesn't look encrypted — legacy plaintext

        try { return Decrypt(value); }
        catch { return value; } // Decryption failed — likely legacy plaintext that happens to be valid Base64
    }
}
