using Central.Licensing;

namespace Central.Tests.Enterprise;

public class LicenseKeyTests
{
    private static (string PrivateKey, string PublicKey) _keys = LicenseKeyService.GenerateKeyPair();

    [Fact]
    public void GenerateKeyPair_ProducesValidKeys()
    {
        var (privateKey, publicKey) = LicenseKeyService.GenerateKeyPair();
        Assert.NotNull(privateKey);
        Assert.NotNull(publicKey);
        Assert.Contains("PRIVATE KEY", privateKey);
        Assert.Contains("PUBLIC KEY", publicKey);
    }

    [Fact]
    public void ValidateLicense_MalformedKey_ReturnsInvalid()
    {
        var svc = new LicenseKeyService("");
        svc.LoadKeys(_keys.PrivateKey, _keys.PublicKey);

        var result = svc.ValidateLicense("not-a-real-license-key", "hw-123");
        Assert.False(result.IsValid);
        Assert.Contains("Malformed", result.ErrorMessage);
    }

    [Fact]
    public void ValidateLicense_SinglePart_ReturnsInvalid()
    {
        var svc = new LicenseKeyService("");
        svc.LoadKeys(_keys.PrivateKey, _keys.PublicKey);

        var result = svc.ValidateLicense("singlepartwithoutdot", "hw-123");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateLicense_EmptyKey_ReturnsInvalid()
    {
        var svc = new LicenseKeyService("");
        svc.LoadKeys(_keys.PrivateKey, _keys.PublicKey);

        var result = svc.ValidateLicense("", "hw-123");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateLicense_InvalidBase64_ReturnsInvalid()
    {
        var svc = new LicenseKeyService("");
        svc.LoadKeys(_keys.PrivateKey, _keys.PublicKey);

        var result = svc.ValidateLicense("!!!invalid!!!.!!!base64!!!", "hw-123");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateLicense_TamperedPayload_InvalidSignature()
    {
        var svc = new LicenseKeyService("");
        svc.LoadKeys(_keys.PrivateKey, _keys.PublicKey);

        // Create a valid-looking but tampered license
        var fakePayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("""{"TenantId":"x"}"""));
        var fakeSig = Convert.ToBase64String(new byte[256]);
        var result = svc.ValidateLicense($"{fakePayload}.{fakeSig}", "hw-123");
        Assert.False(result.IsValid);
        Assert.Contains("Invalid signature", result.ErrorMessage);
    }

    [Fact]
    public void LicensePayload_Defaults()
    {
        var p = new LicensePayload();
        Assert.Equal("", p.TenantId);
        Assert.Equal("", p.HardwareId);
        Assert.Null(p.Modules);
        Assert.Null(p.ExpiresAt);
        Assert.Null(p.IssuedAt);
        Assert.Null(p.KeyId);
    }

    [Fact]
    public void LicenseValidationResult_Invalid_Factory()
    {
        var result = LicenseValidationResult.Invalid("test error");
        Assert.False(result.IsValid);
        Assert.Equal("test error", result.ErrorMessage);
        Assert.Empty(result.Modules);
        Assert.Null(result.TenantId);
        Assert.Null(result.ExpiresAt);
    }

    [Fact]
    public void LicenseValidationResult_Valid()
    {
        var result = new LicenseValidationResult
        {
            IsValid = true,
            TenantId = "tenant-1",
            Modules = new[] { "devices", "switches" },
            ExpiresAt = new DateTime(2027, 1, 1)
        };
        Assert.True(result.IsValid);
        Assert.Equal("tenant-1", result.TenantId);
        Assert.Equal(2, result.Modules.Length);
        Assert.Contains("devices", result.Modules);
    }

    [Fact]
    public void LimitCheckResult_Defaults()
    {
        var r = new LimitCheckResult();
        Assert.False(r.IsWithinLimits);
        Assert.Null(r.Reason);
        Assert.Null(r.CurrentUsers);
        Assert.Null(r.MaxUsers);
        Assert.Null(r.CurrentDevices);
        Assert.Null(r.MaxDevices);
    }

    [Fact]
    public void LimitCheckResult_WithLimits()
    {
        var r = new LimitCheckResult
        {
            IsWithinLimits = false,
            Reason = "User limit reached (50)",
            CurrentUsers = 50,
            MaxUsers = 50
        };
        Assert.False(r.IsWithinLimits);
        Assert.Contains("50", r.Reason);
    }

    [Fact]
    public void ValidateLicense_ThrowsIfNoPublicKey()
    {
        var svc = new LicenseKeyService("");
        // No LoadKeys called
        Assert.Throws<InvalidOperationException>(() =>
            svc.ValidateLicense("a.b", "hw-123"));
    }
}
