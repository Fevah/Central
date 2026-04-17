using Central.Engine.Auth;

namespace Central.Tests.Auth;

public class AuthUserTests
{
    [Fact]
    public void AuthUser_Defaults()
    {
        var u = new AuthUser();
        Assert.Equal(0, u.Id);
        Assert.Equal("", u.Username);
        Assert.Equal("", u.DisplayName);
        Assert.Equal(0, u.RoleId);
        Assert.Equal("", u.RoleName);
        Assert.Equal(0, u.Priority);
        Assert.True(u.IsActive);
        Assert.Equal("ActiveDirectory", u.UserType);
        Assert.Equal("", u.PasswordHash);
        Assert.Equal("", u.Salt);
        Assert.Equal("", u.Email);
    }

    [Fact]
    public void AuthUser_SetProperties()
    {
        var u = new AuthUser
        {
            Id = 1,
            Username = "admin",
            DisplayName = "Admin User",
            RoleId = 1,
            RoleName = "Admin",
            Priority = 1000,
            IsActive = true,
            UserType = "ActiveDirectory",
            Email = "admin@corp.local"
        };
        Assert.Equal(1, u.Id);
        Assert.Equal("admin", u.Username);
        Assert.Equal("Admin User", u.DisplayName);
        Assert.Equal("Admin", u.RoleName);
        Assert.Equal(1000, u.Priority);
        Assert.Equal("admin@corp.local", u.Email);
    }

    [Fact]
    public void AuthUser_IsActive_DefaultTrue()
    {
        var u = new AuthUser();
        Assert.True(u.IsActive);
    }

    [Fact]
    public void AuthUser_CanBeDeactivated()
    {
        var u = new AuthUser { IsActive = false };
        Assert.False(u.IsActive);
    }
}
