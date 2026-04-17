using Central.Core.Auth;

namespace Central.Tests.Auth;

public class TotpServiceTests
{
    [Fact]
    public void GenerateSecret_ReturnsBase32()
    {
        var secret = TotpService.GenerateSecret();
        Assert.NotNull(secret);
        Assert.True(secret.Length > 10);
        // Base32 chars only
        Assert.All(secret, c => Assert.Contains(c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567="));
    }

    [Fact]
    public void GenerateQrUri_ValidFormat()
    {
        var secret = TotpService.GenerateSecret();
        var uri = TotpService.GenerateQrUri(secret, "admin@example.com", "Central");
        Assert.StartsWith("otpauth://totp/Central:", uri);
        Assert.Contains($"secret={secret}", uri);
        Assert.Contains("issuer=Central", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }

    [Fact]
    public void GenerateAndVerify_CurrentCode()
    {
        var secret = TotpService.GenerateSecret();
        var code = TotpService.GenerateCurrentCode(secret);
        Assert.Equal(6, code.Length);
        Assert.True(TotpService.VerifyCode(secret, code));
    }

    [Fact]
    public void VerifyCode_WrongCode_ReturnsFalse()
    {
        var secret = TotpService.GenerateSecret();
        Assert.False(TotpService.VerifyCode(secret, "000000"));
    }

    [Fact]
    public void VerifyCode_EmptyInputs_ReturnsFalse()
    {
        Assert.False(TotpService.VerifyCode("", "123456"));
        Assert.False(TotpService.VerifyCode("JBSWY3DPEHPK3PXP", ""));
        Assert.False(TotpService.VerifyCode(null!, null!));
    }

    [Fact]
    public void GenerateRecoveryCodes_ReturnsCorrectCount()
    {
        var codes = TotpService.GenerateRecoveryCodes(8);
        Assert.Equal(8, codes.Count);
        Assert.All(codes, c =>
        {
            Assert.Equal(11, c.Length); // 10 hex chars + 1 dash
            Assert.Contains("-", c);
        });
    }

    [Fact]
    public void GenerateRecoveryCodes_AllUnique()
    {
        var codes = TotpService.GenerateRecoveryCodes(100);
        Assert.Equal(100, codes.Distinct().Count());
    }
}
