using Central.Core.Models;

namespace Central.Tests.Models;

/// <summary>Tests for enterprise enhancement models (Groups, Feature Flags, Security, Billing).</summary>
public class EnterpriseModelsTests
{
    // ── Groups ──

    [Fact]
    public void GroupRecord_Default_IsStatic()
    {
        var g = new GroupRecord();
        Assert.Equal("static", g.GroupType);
        Assert.True(g.IsActive);
        Assert.False(g.IsDynamic);
    }

    [Fact]
    public void GroupRecord_Dynamic_DetectedCorrectly()
    {
        var g = new GroupRecord { GroupType = "dynamic" };
        Assert.True(g.IsDynamic);
    }

    [Fact]
    public void GroupRecord_PropertyChanged_Fires()
    {
        var g = new GroupRecord();
        string? changed = null;
        g.PropertyChanged += (_, e) => changed = e.PropertyName;
        g.Name = "DevOps";
        Assert.Equal(nameof(GroupRecord.Name), changed);
    }

    // ── Feature Flags ──

    [Fact]
    public void FeatureFlag_DefaultState()
    {
        var f = new FeatureFlag();
        Assert.False(f.DefaultEnabled);
        Assert.Equal("", f.FlagKey);
    }

    [Fact]
    public void TenantFeatureFlag_RolloutPct_Default100()
    {
        var f = new TenantFeatureFlag();
        Assert.Equal(100, f.RolloutPct);
    }

    // ── Security ──

    [Fact]
    public void IpAccessRule_DefaultType_Allow()
    {
        var r = new IpAccessRule();
        Assert.Equal("allow", r.RuleType);
        Assert.Equal("api", r.AppliesTo);
        Assert.True(r.IsActive);
    }

    [Fact]
    public void UserSshKey_DefaultActive()
    {
        var k = new UserSshKey();
        Assert.True(k.IsActive);
    }

    [Fact]
    public void DeprovisioningRule_DefaultAction_Disable()
    {
        var r = new DeprovisioningRule();
        Assert.Equal("disable", r.Action);
        Assert.True(r.IsEnabled);
    }

    [Fact]
    public void DomainVerification_DefaultMethod_DnsTxt()
    {
        var d = new DomainVerification();
        Assert.Equal("dns_txt", d.Method);
        Assert.False(d.IsVerified);
    }

    // ── Billing ──

    [Fact]
    public void SubscriptionAddon_DefaultActive()
    {
        var a = new SubscriptionAddon();
        Assert.True(a.IsActive);
    }

    [Fact]
    public void DiscountCode_DefaultType_Percent()
    {
        var d = new DiscountCode();
        Assert.Equal("percent", d.DiscountType);
        Assert.Equal(0, d.TimesUsed);
    }

    [Fact]
    public void UsageQuota_IsExceeded_WhenOverLimit()
    {
        var q = new UsageQuota { LimitValue = 100, CurrentUsage = 150 };
        Assert.True(q.IsExceeded);
        Assert.Equal(150m, q.UsagePct);
    }

    [Fact]
    public void UsageQuota_IsExceeded_WhenExactlyAtLimit()
    {
        var q = new UsageQuota { LimitValue = 100, CurrentUsage = 100 };
        Assert.True(q.IsExceeded);
    }

    [Fact]
    public void UsageQuota_UsagePct_Calculated()
    {
        var q = new UsageQuota { LimitValue = 200, CurrentUsage = 50 };
        Assert.Equal(25m, q.UsagePct);
    }

    [Fact]
    public void UsageQuota_ZeroLimit_NoDivisionError()
    {
        var q = new UsageQuota { LimitValue = 0, CurrentUsage = 10 };
        Assert.Equal(0m, q.UsagePct);
    }

    // ── Team Extensions ──

    [Fact]
    public void TeamResource_DefaultAccessLevel_Read()
    {
        var r = new TeamResource();
        Assert.Equal("read", r.AccessLevel);
    }

    [Fact]
    public void CompanyUserRole_DefaultState()
    {
        var r = new CompanyUserRole();
        Assert.Equal("", r.RoleName);
    }

    // ── Permission Override ──

    [Fact]
    public void UserPermissionOverride_HasGrantOrDeny()
    {
        var o = new UserPermissionOverride { IsGranted = false };
        Assert.False(o.IsGranted);
    }

    // ── Social Auth ──

    [Fact]
    public void SocialProvider_DefaultState()
    {
        var p = new SocialProvider();
        Assert.False(p.IsEnabled);
    }

    // ── Payment Methods ──

    [Fact]
    public void PaymentMethod_DefaultState()
    {
        var pm = new PaymentMethod();
        Assert.False(pm.IsDefault);
        Assert.Equal("", pm.MethodType);
    }
}

/// <summary>Tests for new enterprise permission codes.</summary>
public class EnterprisePermissionCodeTests
{
    [Theory]
    [InlineData(Central.Core.Auth.P.GroupsRead, "groups:read")]
    [InlineData(Central.Core.Auth.P.GroupsWrite, "groups:write")]
    [InlineData(Central.Core.Auth.P.GroupsDelete, "groups:delete")]
    [InlineData(Central.Core.Auth.P.GroupsAssign, "groups:assign")]
    [InlineData(Central.Core.Auth.P.FeaturesRead, "features:read")]
    [InlineData(Central.Core.Auth.P.FeaturesWrite, "features:write")]
    [InlineData(Central.Core.Auth.P.SecurityIpRules, "security:ip_rules")]
    [InlineData(Central.Core.Auth.P.SecurityKeys, "security:keys")]
    [InlineData(Central.Core.Auth.P.SecurityDeprovision, "security:deprovision")]
    [InlineData(Central.Core.Auth.P.SecurityDomains, "security:domains")]
    [InlineData(Central.Core.Auth.P.BillingRead, "billing:read")]
    [InlineData(Central.Core.Auth.P.BillingWrite, "billing:write")]
    [InlineData(Central.Core.Auth.P.BillingDiscount, "billing:discount")]
    [InlineData(Central.Core.Auth.P.BillingInvoice, "billing:invoice")]
    public void PermissionCode_HasCorrectValue(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
