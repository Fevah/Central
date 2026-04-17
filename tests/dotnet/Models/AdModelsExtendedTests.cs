using Central.Engine.Models;

namespace Central.Tests.Models;

public class AdModelsExtendedTests
{
    // ── AdConfig ──

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

    [Fact]
    public void AdConfig_IsConfigured_False_WhenEmptyDomain()
    {
        Assert.False(new AdConfig { Domain = "" }.IsConfigured);
    }

    [Fact]
    public void AdConfig_IsConfigured_False_WhenWhitespaceDomain()
    {
        Assert.False(new AdConfig { Domain = "   " }.IsConfigured);
    }

    [Fact]
    public void AdConfig_IsConfigured_True_WhenDomainSet()
    {
        Assert.True(new AdConfig { Domain = "corp.local" }.IsConfigured);
    }

    [Fact]
    public void AdConfig_AllProperties()
    {
        var c = new AdConfig
        {
            Domain = "corp.local",
            OuFilter = "OU=Users,DC=corp,DC=local",
            ServiceAccount = "svc_central",
            ServicePassword = "secret",
            UseSsl = true
        };
        Assert.Equal("corp.local", c.Domain);
        Assert.Equal("OU=Users,DC=corp,DC=local", c.OuFilter);
        Assert.Equal("svc_central", c.ServiceAccount);
        Assert.Equal("secret", c.ServicePassword);
        Assert.True(c.UseSsl);
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
        Assert.Equal("", u.Department);
        Assert.Equal("", u.Title);
        Assert.Equal("", u.Phone);
        Assert.Equal("", u.Mobile);
        Assert.Equal("", u.Company);
        Assert.False(u.Enabled);
        Assert.Equal("", u.DistinguishedName);
        Assert.Equal("", u.LoginName);
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
            Email = "jsmith@corp.local",
            Department = "IT",
            Title = "Engineer",
            Phone = "+44 1234",
            Mobile = "+44 7777",
            Company = "Corp Ltd",
            Enabled = true,
            DistinguishedName = "CN=John Smith,OU=Users,DC=corp,DC=local",
            LoginName = "jsmith@corp.local",
            IsImported = true
        };
        Assert.Equal("abc-123", u.ObjectGuid);
        Assert.Equal("jsmith", u.SamAccountName);
        Assert.Equal("John Smith", u.DisplayName);
        Assert.True(u.Enabled);
        Assert.True(u.IsImported);
    }

    [Fact]
    public void AdUser_IsImported_DefaultFalse()
    {
        Assert.False(new AdUser().IsImported);
    }

    [Fact]
    public void AdUser_Enabled_DefaultFalse()
    {
        Assert.False(new AdUser().Enabled);
    }
}
