using Central.Core.Services;

namespace Central.Tests.Services;

public class EnvironmentServiceTests
{
    [Fact]
    public void EnvironmentProfile_TypeColor_LiveGreen()
    {
        var p = new EnvironmentProfile { Type = "live" };
        Assert.Equal("#22C55E", p.TypeColor);
    }

    [Fact]
    public void EnvironmentProfile_TypeColor_TestAmber()
    {
        var p = new EnvironmentProfile { Type = "test" };
        Assert.Equal("#F59E0B", p.TypeColor);
    }

    [Fact]
    public void EnvironmentProfile_TypeColor_DevBlue()
    {
        var p = new EnvironmentProfile { Type = "dev" };
        Assert.Equal("#3B82F6", p.TypeColor);
    }

    [Fact]
    public void EnvironmentProfile_TypeColor_UnknownGrey()
    {
        var p = new EnvironmentProfile { Type = "staging" };
        Assert.Equal("#6B7280", p.TypeColor);
    }

    [Theory]
    [InlineData("live", "LIVE")]
    [InlineData("test", "TEST")]
    [InlineData("dev", "DEV")]
    public void EnvironmentProfile_TypeLabel_MapsCorrectly(string type, string expected)
    {
        var p = new EnvironmentProfile { Type = type };
        Assert.Equal(expected, p.TypeLabel);
    }

    [Fact]
    public void EnvironmentProfile_TypeLabel_Unknown_Uppercased()
    {
        var p = new EnvironmentProfile { Type = "staging" };
        Assert.Equal("STAGING", p.TypeLabel);
    }

    [Fact]
    public void EnvironmentProfile_Defaults()
    {
        var p = new EnvironmentProfile();
        Assert.Equal("", p.Name);
        Assert.Equal("dev", p.Type);
        Assert.Equal("", p.ApiUrl);
        Assert.Null(p.SignalRUrl);
        Assert.Null(p.CertFingerprint);
        Assert.False(p.IsActive);
        Assert.Null(p.TenantSlug);
    }
}
