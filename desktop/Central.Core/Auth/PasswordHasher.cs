using System.Security.Cryptography;
using System.Text;

namespace Central.Core.Auth;

/// <summary>
/// SHA256-based password hasher with per-user salt.
/// Follows TotalLink's PasswordHasher pattern.
/// </summary>
public static class PasswordHasher
{
    /// <summary>Generate a cryptographically random salt (Base64, 32 bytes).</summary>
    public static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Hash a password with the given salt using SHA256.</summary>
    public static string Hash(string password, string salt)
    {
        var input = Encoding.UTF8.GetBytes(password + salt);
        var hash = SHA256.HashData(input);
        return Convert.ToBase64String(hash);
    }

    /// <summary>Verify a password against a stored hash and salt.</summary>
    public static bool Verify(string password, string salt, string storedHash)
        => Hash(password, salt) == storedHash;
}
