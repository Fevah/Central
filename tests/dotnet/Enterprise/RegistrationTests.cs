using Central.Licensing;

namespace Central.Tests.Enterprise;

public class RegistrationTests
{
    [Fact]
    public void RegistrationRequest_Defaults()
    {
        var r = new RegistrationRequest();
        Assert.Equal("", r.Email);
        Assert.Equal("", r.Password);
        Assert.Equal("", r.CompanyName);
        Assert.Null(r.DisplayName);
    }

    [Fact]
    public void RegistrationResult_Fail_Factory()
    {
        var result = RegistrationResult.Fail("Bad email");
        Assert.False(result.Success);
        Assert.Equal("Bad email", result.ErrorMessage);
        Assert.Null(result.UserId);
        Assert.Null(result.TenantId);
        Assert.Null(result.TenantSlug);
        Assert.Null(result.VerifyToken);
    }

    [Fact]
    public void RegistrationResult_Success()
    {
        var result = new RegistrationResult
        {
            Success = true,
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            TenantSlug = "acme-corp",
            VerifyToken = "abc123"
        };
        Assert.True(result.Success);
        Assert.Equal("acme-corp", result.TenantSlug);
        Assert.NotNull(result.UserId);
        Assert.NotNull(result.TenantId);
    }
}
