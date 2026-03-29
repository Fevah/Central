using Central.Security;

namespace Central.Tests.Enterprise;

public class SecurityTests
{
    [Fact]
    public void CanAccessRow_NoPolicies_Allowed()
    {
        var engine = new SecurityPolicyEngine();
        Assert.True(engine.CanAccessRow("acme", "Device", new SecurityContext(), new()));
    }

    [Fact]
    public void CanAccessRow_DenyPolicy_Blocked()
    {
        var engine = new SecurityPolicyEngine();
        engine.LoadPolicies("acme", new[]
        {
            new SecurityPolicy
            {
                EntityType = "Device", PolicyType = "row", Effect = "deny",
                Conditions = new() { ["department"] = "HR" }
            }
        });

        var hrUser = new SecurityContext { Department = "HR" };
        Assert.False(engine.CanAccessRow("acme", "Device", hrUser, new()));

        var itUser = new SecurityContext { Department = "IT" };
        Assert.True(engine.CanAccessRow("acme", "Device", itUser, new()));
    }

    [Fact]
    public void GetHiddenFields_FieldPolicy()
    {
        var engine = new SecurityPolicyEngine();
        engine.LoadPolicies("acme", new[]
        {
            new SecurityPolicy
            {
                EntityType = "User", PolicyType = "field",
                Conditions = new() { ["role"] = "!Admin" }, // non-admins
                HiddenFields = new[] { "salary", "ssn" }
            }
        });

        var viewerHidden = engine.GetHiddenFields("acme", "User", new SecurityContext { Role = "Viewer" });
        Assert.Contains("salary", viewerHidden);
        Assert.Contains("ssn", viewerHidden);

        var adminHidden = engine.GetHiddenFields("acme", "User", new SecurityContext { Role = "Admin" });
        Assert.Empty(adminHidden);
    }

    [Fact]
    public void FilterFields_RemovesHidden()
    {
        var engine = new SecurityPolicyEngine();
        engine.LoadPolicies("acme", new[]
        {
            new SecurityPolicy
            {
                EntityType = "User", PolicyType = "field",
                Conditions = new() { ["role"] = "Viewer" },
                HiddenFields = new[] { "password_hash", "salt" }
            }
        });

        var record = new Dictionary<string, object?>
        {
            ["username"] = "john", ["email"] = "john@test.com",
            ["password_hash"] = "secret", ["salt"] = "abc"
        };

        var filtered = engine.FilterFields("acme", "User", new SecurityContext { Role = "Viewer" }, record);
        Assert.Equal(2, filtered.Count);
        Assert.Contains("username", filtered.Keys);
        Assert.DoesNotContain("password_hash", filtered.Keys);
    }

    [Fact]
    public void SecurityPolicy_Priority_FirstMatchWins()
    {
        var engine = new SecurityPolicyEngine();
        engine.LoadPolicies("acme", new[]
        {
            new SecurityPolicy { EntityType = "Device", PolicyType = "row", Effect = "deny", Priority = 10, Conditions = new() { ["department"] = "HR" } },
            new SecurityPolicy { EntityType = "Device", PolicyType = "row", Effect = "allow", Priority = 20, Conditions = new() { ["department"] = "HR" } }
        });

        // Priority 10 (deny) matches first
        Assert.False(engine.CanAccessRow("acme", "Device", new SecurityContext { Department = "HR" }, new()));
    }

    [Fact]
    public void SecurityContext_Defaults()
    {
        var ctx = new SecurityContext();
        Assert.Equal("", ctx.Username);
        Assert.Equal("", ctx.Role);
        Assert.Equal("internal", ctx.SecurityClearance);
    }
}
