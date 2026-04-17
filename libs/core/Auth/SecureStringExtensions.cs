using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Central.Core.Auth;

/// <summary>
/// SecureString extensions for safe password handling.
/// Plaintext is only marshalled at hash time and immediately zeroed.
/// </summary>
public static class SecureStringExtensions
{
    /// <summary>Convert a plaintext string to a read-only SecureString.</summary>
    public static SecureString ToSecureString(this string plain)
    {
        var secure = new SecureString();
        foreach (var c in plain) secure.AppendChar(c);
        secure.MakeReadOnly();
        return secure;
    }

    /// <summary>
    /// Marshal SecureString to plaintext, execute action, then zero the buffer.
    /// Use sparingly — only at the point where you must produce a hash.
    /// </summary>
    public static string ToPlainText(this SecureString secure)
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secure);
            return Marshal.PtrToStringUni(ptr) ?? "";
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    /// <summary>
    /// Hash a SecureString password with salt using Argon2id.
    /// Plaintext only exists in memory briefly and is not interned.
    /// </summary>
    public static string ToPasswordHash(this SecureString secure, string salt)
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secure);
            var plain = Marshal.PtrToStringUni(ptr) ?? "";
            return PasswordHasher.Hash(plain, salt);
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }

    /// <summary>Verify a SecureString password against a stored hash + salt.</summary>
    public static bool VerifyHash(this SecureString secure, string salt, string storedHash)
    {
        var ptr = IntPtr.Zero;
        try
        {
            ptr = Marshal.SecureStringToGlobalAllocUnicode(secure);
            var plain = Marshal.PtrToStringUni(ptr) ?? "";
            return PasswordHasher.Verify(plain, salt, storedHash);
        }
        finally
        {
            if (ptr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(ptr);
        }
    }
}
