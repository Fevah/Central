using Central.Core.Models;

namespace Central.Tests.Models;

public class RoleSiteAccessTests
{
    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var rsa = new RoleSiteAccess();
        Assert.Equal("", rsa.Building);
        Assert.True(rsa.Allowed); // default allowed = true
    }

    // ── PropertyChanged ──

    [Fact]
    public void PropertyChanged_Building_Fires()
    {
        var rsa = new RoleSiteAccess();
        string? changed = null;
        rsa.PropertyChanged += (_, e) => changed = e.PropertyName;
        rsa.Building = "MEP-91";
        Assert.Equal("Building", changed);
        Assert.Equal("MEP-91", rsa.Building);
    }

    [Fact]
    public void PropertyChanged_Allowed_Fires()
    {
        var rsa = new RoleSiteAccess();
        string? changed = null;
        rsa.PropertyChanged += (_, e) => changed = e.PropertyName;
        rsa.Allowed = false;
        Assert.Equal("Allowed", changed);
        Assert.False(rsa.Allowed);
    }

    [Fact]
    public void AllProperties_FirePropertyChanged()
    {
        var rsa = new RoleSiteAccess();
        var changed = new List<string>();
        rsa.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        rsa.Building = "MEP-92";
        rsa.Allowed = false;

        Assert.Equal(2, changed.Count);
        Assert.Contains("Building", changed);
        Assert.Contains("Allowed", changed);
    }

    [Fact]
    public void SetAllowed_True_AfterFalse()
    {
        var rsa = new RoleSiteAccess { Allowed = false };
        Assert.False(rsa.Allowed);
        rsa.Allowed = true;
        Assert.True(rsa.Allowed);
    }

    [Fact]
    public void Building_CanBeSetToEmptyString()
    {
        var rsa = new RoleSiteAccess { Building = "MEP-91" };
        rsa.Building = "";
        Assert.Equal("", rsa.Building);
    }
}
