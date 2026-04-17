using Central.Engine.Auth;
using Central.Engine.Models;

namespace Central.Tests.Auth;

public class AuthFrameworkTests
{
    // ── AuthenticationResult ──

    [Fact]
    public void AuthenticationResult_Fail_ReturnsUnsuccessful()
    {
        var result = AuthenticationResult.Fail("bad password");
        Assert.False(result.Success);
        Assert.Equal("bad password", result.ErrorMessage);
    }

    [Fact]
    public void AuthenticationResult_Ok_ReturnsSuccessful()
    {
        var result = AuthenticationResult.Ok("admin", "Admin User", "admin@example.com");
        Assert.True(result.Success);
        Assert.Equal("admin", result.Username);
        Assert.Equal("Admin User", result.DisplayName);
        Assert.Equal("admin@example.com", result.Email);
    }

    [Fact]
    public void AuthenticationResult_Claims_DefaultEmpty()
    {
        var result = new AuthenticationResult { Success = true };
        Assert.NotNull(result.Claims);
        Assert.Empty(result.Claims);
    }

    // ── UserTypes ──

    [Fact]
    public void UserTypes_IsProtected_SystemAndService()
    {
        Assert.True(UserTypes.IsProtected("System"));
        Assert.True(UserTypes.IsProtected("Service"));
        Assert.False(UserTypes.IsProtected("Standard"));
        Assert.False(UserTypes.IsProtected("ActiveDirectory"));
        Assert.False(UserTypes.IsProtected("Admin"));
        Assert.False(UserTypes.IsProtected(null));
    }

    [Fact]
    public void UserTypes_All_Contains5Types()
    {
        Assert.Equal(5, UserTypes.All.Length);
        Assert.Contains("System", UserTypes.All);
        Assert.Contains("Admin", UserTypes.All);
        Assert.Contains("Standard", UserTypes.All);
        Assert.Contains("ActiveDirectory", UserTypes.All);
        Assert.Contains("Service", UserTypes.All);
    }

    // ── AuthStates ──

    [Fact]
    public void AuthStates_HasAllProviderValues()
    {
        Assert.Equal(0, (int)AuthStates.NotAuthenticated);
        Assert.Equal(1, (int)AuthStates.Windows);
        Assert.Equal(2, (int)AuthStates.Offline);
        Assert.Equal(3, (int)AuthStates.Password);
        Assert.Equal(4, (int)AuthStates.EntraId);
        Assert.Equal(5, (int)AuthStates.Okta);
        Assert.Equal(6, (int)AuthStates.Saml);
        Assert.Equal(7, (int)AuthStates.Local);
        Assert.Equal(8, (int)AuthStates.ApiToken);
    }

    // ── SecureStringExtensions ──

    [Fact]
    public void SecureString_ToPasswordHash_ProducesConsistentHash()
    {
        var secure1 = "testpassword".ToSecureString();
        var secure2 = "testpassword".ToSecureString();

        var hash1 = secure1.ToPasswordHash("salt123");
        var hash2 = secure2.ToPasswordHash("salt123");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void SecureString_DifferentSalt_DifferentHash()
    {
        var secure = "testpassword".ToSecureString();

        var hash1 = secure.ToPasswordHash("salt1");
        var hash2 = secure.ToPasswordHash("salt2");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void SecureString_VerifyHash_CorrectPassword()
    {
        var secure = "mypassword".ToSecureString();
        var hash = secure.ToPasswordHash("mysalt");

        var verifySecure = "mypassword".ToSecureString();
        Assert.True(verifySecure.VerifyHash("mysalt", hash));
    }

    [Fact]
    public void SecureString_VerifyHash_WrongPassword()
    {
        var secure = "correct".ToSecureString();
        var hash = secure.ToPasswordHash("salt");

        var wrong = "incorrect".ToSecureString();
        Assert.False(wrong.VerifyHash("salt", hash));
    }

    [Fact]
    public void SecureString_ToPlainText_RoundTrips()
    {
        var original = "hello world 123!@#";
        var secure = original.ToSecureString();
        var plain = secure.ToPlainText();
        Assert.Equal(original, plain);
    }

    // ── IdentityProviderConfig ──

    [Fact]
    public void IdentityProviderConfig_GetConfigValue_ReturnsDefault()
    {
        var config = new IdentityProviderConfig();
        Assert.Equal("fallback", config.GetConfigValue("missing", "fallback"));
    }

    [Fact]
    public void IdentityProviderConfig_GetConfigValue_ReturnsValue()
    {
        var config = new IdentityProviderConfig();
        config.Config["tenant_id"] = "abc123";
        Assert.Equal("abc123", config.GetConfigValue("tenant_id"));
    }

    // ── ClaimMapping ──

    [Fact]
    public void ClaimMapping_PropertyChanged_Fires()
    {
        var mapping = new ClaimMapping();
        bool fired = false;
        mapping.PropertyChanged += (_, _) => fired = true;

        mapping.ClaimType = "groups";
        Assert.True(fired);
    }

    // ── AppUser Extensions ──

    [Fact]
    public void AppUser_IsProtected_SystemUser()
    {
        var user = new AppUser { UserType = "System" };
        Assert.True(user.IsProtected);
        Assert.True(user.IsSystemUser);
    }

    [Fact]
    public void AppUser_IsProtected_ServiceUser()
    {
        var user = new AppUser { UserType = "Service" };
        Assert.True(user.IsProtected);
    }

    [Fact]
    public void AppUser_NotProtected_StandardUser()
    {
        var user = new AppUser { UserType = "Standard" };
        Assert.False(user.IsProtected);
    }

    [Fact]
    public void AppUser_IsAdUser()
    {
        var user = new AppUser { UserType = "ActiveDirectory" };
        Assert.True(user.IsAdUser);
    }

    [Fact]
    public void AppUser_ExtendedFields_DefaultEmpty()
    {
        var user = new AppUser();
        Assert.Equal("", user.Department);
        Assert.Equal("", user.Title);
        Assert.Equal("", user.Phone);
        Assert.Equal("", user.Mobile);
        Assert.Equal("", user.Company);
        Assert.Equal("", user.AdGuid);
        Assert.Null(user.LastAdSync);
    }
}
