using Central.Engine.Models;

namespace Central.Tests.Models;

public class AdModelsTests
{
    // ── AdConfig ──

    [Fact]
    public void AdConfig_IsConfigured_WithDomain_True()
    {
        var c = new AdConfig { Domain = "corp.local" };
        Assert.True(c.IsConfigured);
    }

    [Fact]
    public void AdConfig_IsConfigured_EmptyDomain_False()
    {
        var c = new AdConfig { Domain = "" };
        Assert.False(c.IsConfigured);
    }

    [Fact]
    public void AdConfig_IsConfigured_WhitespaceDomain_False()
    {
        var c = new AdConfig { Domain = "   " };
        Assert.False(c.IsConfigured);
    }

    [Fact]
    public void AdConfig_Defaults()
    {
        var c = new AdConfig();
        Assert.Equal("", c.Domain);
        Assert.Equal("", c.OuFilter);
        Assert.Equal("", c.ServiceAccount);
        Assert.Equal("", c.ServicePassword);
        Assert.False(c.UseSsl);
    }

    // ── AdUser ──

    [Fact]
    public void AdUser_Defaults()
    {
        var u = new AdUser();
        Assert.Equal("", u.ObjectGuid);
        Assert.Equal("", u.SamAccountName);
        Assert.Equal("", u.DisplayName);
        Assert.Equal("", u.Email);
        Assert.False(u.Enabled);
        Assert.False(u.IsImported);
    }

    [Fact]
    public void AdUser_SetAllProperties()
    {
        var u = new AdUser
        {
            ObjectGuid = "abc-123",
            SamAccountName = "jsmith",
            DisplayName = "John Smith",
            Email = "john@corp.local",
            Department = "IT",
            Title = "Engineer",
            Phone = "123-456",
            Mobile = "789-012",
            Company = "Corp",
            Enabled = true,
            DistinguishedName = "CN=John,OU=Users,DC=corp,DC=local",
            LoginName = "jsmith@corp.local",
            IsImported = true
        };
        Assert.Equal("abc-123", u.ObjectGuid);
        Assert.Equal("jsmith", u.SamAccountName);
        Assert.True(u.Enabled);
        Assert.True(u.IsImported);
    }
}
