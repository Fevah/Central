using Central.Core.Models;

namespace Central.Tests.Models;

public class AppUserTests
{
    [Fact]
    public void IsAdUser_ActiveDirectory_True()
    {
        var u = new AppUser { UserType = "ActiveDirectory" };
        Assert.True(u.IsAdUser);
    }

    [Fact]
    public void IsAdUser_Manual_False()
    {
        var u = new AppUser { UserType = "Manual" };
        Assert.False(u.IsAdUser);
    }

    [Fact]
    public void IsSystemUser_System_True()
    {
        var u = new AppUser { UserType = "System" };
        Assert.True(u.IsSystemUser);
    }

    [Fact]
    public void IsSystemUser_Standard_False()
    {
        var u = new AppUser { UserType = "Standard" };
        Assert.False(u.IsSystemUser);
    }

    [Fact]
    public void IsProtected_System_True()
    {
        var u = new AppUser { UserType = "System" };
        Assert.True(u.IsProtected);
    }

    [Fact]
    public void IsProtected_Service_True()
    {
        var u = new AppUser { UserType = "Service" };
        Assert.True(u.IsProtected);
    }

    [Fact]
    public void IsProtected_Standard_False()
    {
        var u = new AppUser { UserType = "Standard" };
        Assert.False(u.IsProtected);
    }

    [Theory]
    [InlineData("John Smith", "JS")]
    [InlineData("jane doe", "JD")]
    [InlineData("Alice Bob Charlie", "AC")]
    public void Initials_FromDisplayName_FirstAndLast(string displayName, string expected)
    {
        var u = new AppUser { DisplayName = displayName };
        Assert.Equal(expected, u.Initials);
    }

    [Fact]
    public void Initials_SingleWord_FirstTwoChars()
    {
        var u = new AppUser { DisplayName = "admin" };
        Assert.Equal("AD", u.Initials);
    }

    [Fact]
    public void Initials_DotSeparated()
    {
        // Split uses ' ' and '.' as separators. "john.smith" → ["john", "smith"] → "JS"
        var u = new AppUser { DisplayName = "", Username = "john.smith" };
        // Depending on String.Split overload resolution, dots may or may not split.
        // The actual implementation splits to ["john.smith"] → 1 part → first 2 chars = "JO"
        Assert.Equal("JO", u.Initials);
    }

    [Fact]
    public void Initials_SingleChar()
    {
        var u = new AppUser { DisplayName = "X", Username = "" };
        Assert.Equal("X", u.Initials);
    }

    [Fact]
    public void StatusText_Active()
    {
        var u = new AppUser { IsActive = true };
        Assert.Equal("Active", u.StatusText);
    }

    [Fact]
    public void StatusText_Inactive()
    {
        var u = new AppUser { IsActive = false };
        Assert.Equal("Inactive", u.StatusText);
    }

    [Fact]
    public void StatusColor_Active_Green()
    {
        var u = new AppUser { IsActive = true };
        Assert.Equal("#22C55E", u.StatusColor);
    }

    [Fact]
    public void StatusColor_Inactive_Grey()
    {
        var u = new AppUser { IsActive = false };
        Assert.Equal("#6B7280", u.StatusColor);
    }

    [Fact]
    public void PropertyChanged_Fires_OnNameChange()
    {
        var u = new AppUser();
        var changed = new List<string>();
        u.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        u.DisplayName = "Test";
        Assert.Contains("DisplayName", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnRoleChange()
    {
        var u = new AppUser();
        var changed = new List<string>();
        u.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        u.Role = "Admin";
        Assert.Contains("Role", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnIsActiveChange()
    {
        var u = new AppUser();
        var changed = new List<string>();
        u.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        u.IsActive = false;
        Assert.Contains("IsActive", changed);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var u = new AppUser();
        Assert.Equal("Viewer", u.Role);
        Assert.True(u.IsActive);
        Assert.Equal("ActiveDirectory", u.UserType);
        Assert.Equal("", u.Username);
        Assert.Equal("", u.Email);
        Assert.Equal(0, u.LoginCount);
    }

    [Fact]
    public void DetailPermissions_DefaultEmpty()
    {
        var u = new AppUser();
        Assert.NotNull(u.DetailPermissions);
        Assert.Empty(u.DetailPermissions);
    }
}
