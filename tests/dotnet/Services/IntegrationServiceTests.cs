using Central.Engine.Services;

namespace Central.Tests.Services;

public class IntegrationServiceTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var svc = new IntegrationService("ManageEngine");
        Assert.Equal("ManageEngine", svc.IntegrationName);
    }

    [Fact]
    public void IsConfigured_False_WhenNoCredentials()
    {
        var svc = new IntegrationService("test");
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_True_WhenClientIdAndRefreshToken()
    {
        var svc = new IntegrationService("test")
        {
            ClientId = "client-123",
            RefreshToken = "rt-abc"
        };
        Assert.True(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_False_MissingRefreshToken()
    {
        var svc = new IntegrationService("test")
        {
            ClientId = "client-123"
        };
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void IsConfigured_False_MissingClientId()
    {
        var svc = new IntegrationService("test")
        {
            RefreshToken = "rt-abc"
        };
        Assert.False(svc.IsConfigured);
    }

    [Fact]
    public void HasValidToken_False_Initially()
    {
        var svc = new IntegrationService("test");
        Assert.False(svc.HasValidToken);
    }

    [Fact]
    public void DefaultProperties()
    {
        var svc = new IntegrationService("test");
        Assert.Equal("", svc.OAuthUrl);
        Assert.Equal("", svc.BaseUrl);
        Assert.Null(svc.ClientId);
        Assert.Null(svc.ClientSecret);
        Assert.Null(svc.RefreshToken);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsNull_WhenNoRefreshToken()
    {
        var svc = new IntegrationService("test");
        var token = await svc.GetAccessTokenAsync();
        Assert.Null(token);
    }
}
