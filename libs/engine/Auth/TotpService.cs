using OtpNet;

namespace Central.Engine.Auth;

/// <summary>
/// TOTP (Time-based One-Time Password) service for MFA enrollment and verification.
/// Uses RFC 6238 standard (Google Authenticator, Microsoft Authenticator, Authy compatible).
/// </summary>
public static class TotpService
{
    private const int SecretLength = 20; // 160 bits
    private const int CodeDigits = 6;
    private const int StepSeconds = 30;
    private const int WindowSize = 1; // allow 1 step before/after for clock skew

    /// <summary>Generate a new TOTP secret for MFA enrollment.</summary>
    public static string GenerateSecret()
    {
        var secret = KeyGeneration.GenerateRandomKey(SecretLength);
        return Base32Encoding.ToString(secret);
    }

    /// <summary>
    /// Generate a QR code URI for enrollment in an authenticator app.
    /// Format: otpauth://totp/{issuer}:{account}?secret={secret}&issuer={issuer}&digits=6&period=30
    /// </summary>
    public static string GenerateQrUri(string secret, string accountName, string issuer = "Central")
    {
        var encoded = Uri.EscapeDataString(accountName);
        var issuerEncoded = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{issuerEncoded}:{encoded}?secret={secret}&issuer={issuerEncoded}&digits={CodeDigits}&period={StepSeconds}";
    }

    /// <summary>Verify a TOTP code against a secret. Allows ±1 time step for clock skew.</summary>
    public static bool VerifyCode(string secret, string code)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(code)) return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes, step: StepSeconds, totpSize: CodeDigits);
            return totp.VerifyTotp(code, out _, new VerificationWindow(WindowSize, WindowSize));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Generate the current TOTP code (for testing/debug only).</summary>
    public static string GenerateCurrentCode(string secret)
    {
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes, step: StepSeconds, totpSize: CodeDigits);
        return totp.ComputeTotp();
    }

    /// <summary>Generate recovery codes (one-time use backup codes).</summary>
    public static List<string> GenerateRecoveryCodes(int count = 8)
    {
        var codes = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var bytes = new byte[5];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            codes.Add(Convert.ToHexString(bytes).ToLowerInvariant().Insert(5, "-"));
        }
        return codes;
    }
}
