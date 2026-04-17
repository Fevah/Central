using Central.Engine.Auth;

namespace Central.Tests.Auth;

public class AuthStatesExtendedTests
{
    [Fact]
    public void NotAuthenticated_IsZero()
    {
        Assert.Equal(0, (int)AuthStates.NotAuthenticated);
    }

    [Fact]
    public void Windows_IsOne()
    {
        Assert.Equal(1, (int)AuthStates.Windows);
    }

    [Fact]
    public void Offline_IsTwo()
    {
        Assert.Equal(2, (int)AuthStates.Offline);
    }

    [Fact]
    public void Password_IsThree()
    {
        Assert.Equal(3, (int)AuthStates.Password);
    }

    [Fact]
    public void EntraId_IsFour()
    {
        Assert.Equal(4, (int)AuthStates.EntraId);
    }

    [Fact]
    public void Okta_IsFive()
    {
        Assert.Equal(5, (int)AuthStates.Okta);
    }

    [Fact]
    public void Saml_IsSix()
    {
        Assert.Equal(6, (int)AuthStates.Saml);
    }

    [Fact]
    public void Local_IsSeven()
    {
        Assert.Equal(7, (int)AuthStates.Local);
    }

    [Fact]
    public void ApiToken_IsEight()
    {
        Assert.Equal(8, (int)AuthStates.ApiToken);
    }

    [Fact]
    public void AllStates_Defined()
    {
        var values = Enum.GetValues<AuthStates>();
        Assert.Equal(9, values.Length);
    }

    [Theory]
    [InlineData(AuthStates.Windows)]
    [InlineData(AuthStates.Offline)]
    [InlineData(AuthStates.Password)]
    [InlineData(AuthStates.EntraId)]
    [InlineData(AuthStates.Okta)]
    [InlineData(AuthStates.Saml)]
    [InlineData(AuthStates.Local)]
    [InlineData(AuthStates.ApiToken)]
    public void AuthenticatedStates_AreNotZero(AuthStates state)
    {
        Assert.NotEqual(0, (int)state);
    }
}
