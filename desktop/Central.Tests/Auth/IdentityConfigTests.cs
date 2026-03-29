using Central.Core.Auth;

namespace Central.Tests.Auth;

public class IdentityConfigTests
{
    [Fact]
    public void IdentityProviderConfig_GetConfigValue_Exists()
    {
        var cfg = new IdentityProviderConfig();
        cfg.Config["tenant_id"] = "abc-123";
        Assert.Equal("abc-123", cfg.GetConfigValue("tenant_id"));
    }

    [Fact]
    public void IdentityProviderConfig_GetConfigValue_Missing_Default()
    {
        var cfg = new IdentityProviderConfig();
        Assert.Equal("fallback", cfg.GetConfigValue("missing_key", "fallback"));
        Assert.Equal("", cfg.GetConfigValue("missing_key"));
    }

    [Fact]
    public void ClaimMapping_PropertyChanged_AllFields()
    {
        var cm = new ClaimMapping();
        var changed = new List<string>();
        cm.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        cm.ClaimType = "groups";
        cm.ClaimValue = "Admins";
        cm.TargetRole = "Admin";
        cm.Priority = 10;
        cm.IsEnabled = false;

        Assert.Contains("ClaimType", changed);
        Assert.Contains("ClaimValue", changed);
        Assert.Contains("TargetRole", changed);
        Assert.Contains("Priority", changed);
        Assert.Contains("IsEnabled", changed);
    }

    [Fact]
    public void IdpDomainMapping_Defaults()
    {
        var m = new IdpDomainMapping();
        Assert.Equal("", m.EmailDomain);
        Assert.Equal(0, m.ProviderId);
    }

    [Fact]
    public void UserExternalIdentity_Defaults()
    {
        var id = new UserExternalIdentity();
        Assert.Equal("", id.ExternalId);
        Assert.Null(id.ExternalEmail);
        Assert.Null(id.LinkedAt);
    }

    [Fact]
    public void AuthEvent_Defaults()
    {
        var ev = new AuthEvent();
        Assert.Equal("", ev.EventType);
        Assert.Null(ev.ProviderType);
        Assert.False(ev.Success);
    }

    [Fact]
    public void AuthenticationResult_ClaimsOperations()
    {
        var result = new AuthenticationResult
        {
            Success = true,
            Claims = new Dictionary<string, List<string>>
            {
                ["groups"] = new() { "Admins", "Users" },
                ["roles"] = new() { "Admin" }
            }
        };

        Assert.Equal(2, result.Claims["groups"].Count);
        Assert.Contains("Admin", result.Claims["roles"]);
    }

    [Fact]
    public void AuthenticationRequest_Defaults()
    {
        var req = new AuthenticationRequest();
        Assert.Null(req.Username);
        Assert.Null(req.Password);
        Assert.Null(req.Email);
        Assert.Null(req.MfaCode);
    }
}
