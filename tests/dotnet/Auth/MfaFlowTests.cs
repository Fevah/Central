using Central.Engine.Auth;

namespace Central.Tests.Auth;

/// <summary>
/// Tests for the full MFA enrollment and verification flow.
/// Validates TOTP secret generation, QR URI, code verification, and recovery codes.
/// </summary>
public class MfaFlowTests
{
    [Fact]
    public void MfaSetup_GenerateSecret_IsBase32()
    {
        var secret = TotpService.GenerateSecret();
        Assert.NotEmpty(secret);
        // Base32 characters only
        Assert.True(secret.All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567=".Contains(c)));
    }

    [Fact]
    public void MfaSetup_QrUri_ContainsSecret()
    {
        var secret = TotpService.GenerateSecret();
        var uri = TotpService.GenerateQrUri(secret, "user@company.com");

        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains(secret, uri);
        Assert.Contains("user%40company.com", uri);
        Assert.Contains("issuer=Central", uri);
    }

    [Fact]
    public void MfaVerify_ValidCode_ReturnsTrue()
    {
        var secret = TotpService.GenerateSecret();
        var code = TotpService.GenerateCurrentCode(secret);
        Assert.True(TotpService.VerifyCode(secret, code));
    }

    [Fact]
    public void MfaVerify_WrongCode_ReturnsFalse()
    {
        var secret = TotpService.GenerateSecret();
        Assert.False(TotpService.VerifyCode(secret, "000000"));
    }

    [Fact]
    public void MfaVerify_EmptyCode_ReturnsFalse()
    {
        var secret = TotpService.GenerateSecret();
        Assert.False(TotpService.VerifyCode(secret, ""));
    }

    [Fact]
    public void MfaVerify_NullSecret_ReturnsFalse()
    {
        Assert.False(TotpService.VerifyCode(null!, "123456"));
    }

    [Fact]
    public void RecoveryCodes_GeneratesUniqueSet()
    {
        var codes = TotpService.GenerateRecoveryCodes(8);
        Assert.Equal(8, codes.Count);
        Assert.Equal(8, codes.Distinct().Count());
    }

    [Fact]
    public void RecoveryCodes_CorrectFormat()
    {
        var codes = TotpService.GenerateRecoveryCodes(1);
        var code = codes[0];
        // Format: 5 hex chars + dash + 5 hex chars
        Assert.Equal(11, code.Length);
        Assert.Equal('-', code[5]);
    }

    [Fact]
    public void MfaVerify_CodeFromDifferentSecret_ReturnsFalse()
    {
        var secret1 = TotpService.GenerateSecret();
        var secret2 = TotpService.GenerateSecret();
        var code = TotpService.GenerateCurrentCode(secret1);
        Assert.False(TotpService.VerifyCode(secret2, code));
    }

    [Fact]
    public void MfaSetup_SecretCanBeEncrypted()
    {
        CredentialEncryptor.Initialize("test-mfa-key");
        var secret = TotpService.GenerateSecret();
        var encrypted = CredentialEncryptor.Encrypt(secret);
        var decrypted = CredentialEncryptor.Decrypt(encrypted);

        Assert.Equal(secret, decrypted);
        Assert.NotEqual(secret, encrypted);

        // Verify TOTP still works with decrypted secret
        var code = TotpService.GenerateCurrentCode(decrypted);
        Assert.True(TotpService.VerifyCode(decrypted, code));
    }
}
