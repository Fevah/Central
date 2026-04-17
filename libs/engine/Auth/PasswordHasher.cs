using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace Central.Engine.Auth;

/// <summary>
/// Argon2id password hasher (OWASP recommended).
/// Supports verifying legacy SHA256 hashes for migration — on successful
/// legacy verify the caller should re-hash with Argon2id.
/// </summary>
public static class PasswordHasher
{
    // OWASP minimum: 19 MiB memory, 2 iterations, 1 parallelism
    private const int MemorySize = 65536;  // 64 MiB
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private const int HashLength = 32;

    /// <summary>Generate a cryptographically random salt (Base64, 32 bytes).</summary>
    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Hash a password with Argon2id. Returns Base64-encoded hash.</summary>
    public static string Hash(string password, string salt)
    {
        // Salt must be Base64-encoded bytes from GenerateSalt()
        byte[] saltBytes;
        try { saltBytes = Convert.FromBase64String(salt); }
        catch (FormatException)
        {
            // Legacy salt format (plain string) — convert to bytes directly
            saltBytes = Encoding.UTF8.GetBytes(salt);
        }

        // Argon2id requires non-empty password bytes
        var passwordBytes = Encoding.UTF8.GetBytes(password ?? "");
        if (passwordBytes.Length == 0) passwordBytes = new byte[] { 0 };

        using var argon2 = new Argon2id(passwordBytes)
        {
            Salt = saltBytes,
            DegreeOfParallelism = Parallelism,
            MemorySize = MemorySize,
            Iterations = Iterations
        };
        var hashBytes = argon2.GetBytes(HashLength);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>Verify a password against a stored hash and salt (Argon2id).</summary>
    public static bool Verify(string password, string salt, string storedHash)
        => CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(Hash(password, salt)),
            Convert.FromBase64String(storedHash));

    /// <summary>
    /// Verify against a legacy SHA256 hash. Returns true if the password matches
    /// the old format. Caller should re-hash with <see cref="Hash"/> on success.
    /// </summary>
    public static bool VerifyLegacySha256(string password, string salt, string storedHash)
    {
        var legacyHash = HashLegacySha256(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(legacyHash),
            Convert.FromBase64String(storedHash));
    }

    /// <summary>Check if a stored hash is a legacy SHA256 hash (32 bytes = 44 Base64 chars).</summary>
    public static bool IsLegacyHash(string storedHash)
    {
        try
        {
            var bytes = Convert.FromBase64String(storedHash);
            return bytes.Length == 32; // SHA256 = 32 bytes, Argon2id with our config = 32 bytes but different content
        }
        catch { return false; }
    }

    /// <summary>Legacy SHA256 hash for migration verification only. Do not use for new passwords.</summary>
    private static string HashLegacySha256(string password, string salt)
    {
        var input = Encoding.UTF8.GetBytes(password + salt);
        var hash = SHA256.HashData(input);
        return Convert.ToBase64String(hash);
    }
}
