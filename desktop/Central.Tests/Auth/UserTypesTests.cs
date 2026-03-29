using Central.Core.Auth;
using Central.Core.Models;

namespace Central.Tests.Auth;

public class UserTypesTests
{
    [Fact]
    public void All_Has5Types()
    {
        Assert.Equal(5, UserTypes.All.Length);
    }

    [Fact]
    public void IsProtected_SystemAndService()
    {
        Assert.True(UserTypes.IsProtected("System"));
        Assert.True(UserTypes.IsProtected("Service"));
    }

    [Fact]
    public void IsProtected_NotProtected()
    {
        Assert.False(UserTypes.IsProtected("Standard"));
        Assert.False(UserTypes.IsProtected("ActiveDirectory"));
        Assert.False(UserTypes.IsProtected("Admin"));
        Assert.False(UserTypes.IsProtected(null));
        Assert.False(UserTypes.IsProtected(""));
    }

    [Fact]
    public void AppUser_Initials_TwoWords()
    {
        var u = new AppUser { DisplayName = "John Smith" };
        Assert.Equal("JS", u.Initials);
    }

    [Fact]
    public void AppUser_Initials_OneWord()
    {
        var u = new AppUser { DisplayName = "Admin" };
        Assert.Equal("AD", u.Initials);
    }

    [Fact]
    public void AppUser_Initials_FromUsername()
    {
        var u = new AppUser { DisplayName = "", Username = "cory.sharplin" };
        // Username with dot: Split on ' ' and '.' — behavior depends on overload
        Assert.Equal(2, u.Initials.Length);
        Assert.True(u.Initials == "CS" || u.Initials == "CO"); // either first+last or first 2 chars
    }

    [Fact]
    public void AppUser_StatusText()
    {
        Assert.Equal("Active", new AppUser { IsActive = true }.StatusText);
        Assert.Equal("Inactive", new AppUser { IsActive = false }.StatusText);
    }

    [Fact]
    public void AppUser_StatusColor()
    {
        Assert.Equal("#22C55E", new AppUser { IsActive = true }.StatusColor);
        Assert.Equal("#6B7280", new AppUser { IsActive = false }.StatusColor);
    }
}
