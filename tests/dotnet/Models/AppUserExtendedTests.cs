using Central.Engine.Models;

namespace Central.Tests.Models;

public class AppUserExtendedTests
{
    // ── Extended PropertyChanged tests ──

    [Fact]
    public void PropertyChanged_Email_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Email = "test@corp.local";
        Assert.Equal("Email", changed);
    }

    [Fact]
    public void PropertyChanged_Department_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Department = "IT";
        Assert.Equal("Department", changed);
    }

    [Fact]
    public void PropertyChanged_Title_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Title = "Network Engineer";
        Assert.Equal("Title", changed);
    }

    [Fact]
    public void PropertyChanged_Phone_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Phone = "+44 1234 567890";
        Assert.Equal("Phone", changed);
    }

    [Fact]
    public void PropertyChanged_Mobile_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Mobile = "+44 7777 123456";
        Assert.Equal("Mobile", changed);
    }

    [Fact]
    public void PropertyChanged_Company_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Company = "Immunocore";
        Assert.Equal("Company", changed);
    }

    [Fact]
    public void PropertyChanged_AdGuid_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.AdGuid = "abc-123-def";
        Assert.Equal("AdGuid", changed);
    }

    [Fact]
    public void PropertyChanged_LastAdSync_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.LastAdSync = DateTime.UtcNow;
        Assert.Equal("LastAdSync", changed);
    }

    [Fact]
    public void PropertyChanged_LastLoginAt_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.LastLoginAt = DateTime.UtcNow;
        Assert.Equal("LastLoginAt", changed);
    }

    [Fact]
    public void PropertyChanged_LoginCount_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.LoginCount = 42;
        Assert.Equal("LoginCount", changed);
    }

    [Fact]
    public void PropertyChanged_CreatedAt_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.CreatedAt = DateTime.UtcNow;
        Assert.Equal("CreatedAt", changed);
    }

    [Fact]
    public void PropertyChanged_AutoLogin_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.AutoLogin = true;
        Assert.Equal("AutoLogin", changed);
    }

    [Fact]
    public void PropertyChanged_UserType_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.UserType = "Manual";
        Assert.Equal("UserType", changed);
    }

    [Fact]
    public void PropertyChanged_AdSid_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.AdSid = "S-1-5-21-123";
        Assert.Equal("AdSid", changed);
    }

    [Fact]
    public void PropertyChanged_PasswordHash_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.PasswordHash = "abc123hash";
        Assert.Equal("PasswordHash", changed);
    }

    [Fact]
    public void PropertyChanged_Salt_Fires()
    {
        var u = new AppUser();
        string? changed = null;
        u.PropertyChanged += (_, e) => changed = e.PropertyName;
        u.Salt = "random_salt";
        Assert.Equal("Salt", changed);
    }

    // ── Initials edge cases ──

    [Fact]
    public void Initials_FallsBackToUsername_WhenDisplayNameEmpty()
    {
        var u = new AppUser { DisplayName = "", Username = "jsmith" };
        Assert.Equal("JS", u.Initials);
    }

    [Fact]
    public void Initials_TwoWordDisplayName()
    {
        var u = new AppUser { DisplayName = "Alice Wonder" };
        Assert.Equal("AW", u.Initials);
    }

    [Fact]
    public void Initials_ThreeWordDisplayName_UsesFirstAndLast()
    {
        var u = new AppUser { DisplayName = "Alice Bob Wonder" };
        Assert.Equal("AW", u.Initials);
    }

    [Fact]
    public void Initials_SingleCharUsername()
    {
        var u = new AppUser { DisplayName = "", Username = "a" };
        Assert.Equal("A", u.Initials);
    }

    // ── Computed property checks ──

    [Theory]
    [InlineData("ActiveDirectory", true)]
    [InlineData("Manual", false)]
    [InlineData("System", false)]
    [InlineData("", false)]
    public void IsAdUser_VariousTypes(string userType, bool expected)
    {
        var u = new AppUser { UserType = userType };
        Assert.Equal(expected, u.IsAdUser);
    }

    [Theory]
    [InlineData("System", true)]
    [InlineData("ActiveDirectory", false)]
    [InlineData("Manual", false)]
    public void IsSystemUser_VariousTypes(string userType, bool expected)
    {
        var u = new AppUser { UserType = userType };
        Assert.Equal(expected, u.IsSystemUser);
    }

    // ── Nullable datetime fields ──

    [Fact]
    public void NullableDateTimes_DefaultNull()
    {
        var u = new AppUser();
        Assert.Null(u.LastAdSync);
        Assert.Null(u.LastLoginAt);
        Assert.Null(u.CreatedAt);
    }

    [Fact]
    public void NullableDateTimes_CanBeCleared()
    {
        var u = new AppUser { LastLoginAt = DateTime.UtcNow };
        u.LastLoginAt = null;
        Assert.Null(u.LastLoginAt);
    }
}
