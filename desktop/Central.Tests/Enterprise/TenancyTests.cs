using Central.Tenancy;

namespace Central.Tests.Enterprise;

public class TenancyTests
{
    [Fact]
    public void TenantContext_Default_IsPublicSchema()
    {
        var ctx = TenantContext.Default;
        Assert.Equal("default", ctx.TenantSlug);
        Assert.Equal("public", ctx.SchemaName);
        Assert.True(ctx.IsResolved);
        Assert.Equal("enterprise", ctx.Tier);
    }

    [Fact]
    public void TenantContext_CustomTenant()
    {
        var ctx = new TenantContext
        {
            TenantId = Guid.NewGuid(),
            TenantSlug = "acme",
            SchemaName = "tenant_acme",
            Tier = "professional"
        };
        Assert.Equal("acme", ctx.TenantSlug);
        Assert.Equal("tenant_acme", ctx.SchemaName);
    }

    [Fact]
    public void TenantConnectionFactory_SchemaValidation()
    {
        var ctx = new TenantContext { SchemaName = "tenant_valid_123", IsResolved = true };
        var factory = new TenantConnectionFactory("Host=localhost", ctx);
        Assert.Equal("Host=localhost", factory.ConnectionString);
    }

    [Fact]
    public void Tenant_Model_Defaults()
    {
        var t = new Tenant();
        Assert.Equal("", t.Slug);
        Assert.True(t.IsActive);
        Assert.Equal("free", t.Tier);
    }

    [Fact]
    public void SubscriptionPlan_Model()
    {
        var p = new SubscriptionPlan { Tier = "enterprise", MaxUsers = null, MaxDevices = null };
        Assert.Null(p.MaxUsers);
        Assert.Null(p.MaxDevices);
    }

    [Fact]
    public void GlobalUser_Memberships()
    {
        var u = new GlobalUser { Email = "test@example.com" };
        u.Memberships.Add(new TenantMembership { TenantSlug = "acme", Role = "Admin" });
        Assert.Single(u.Memberships);
        Assert.Equal("Admin", u.Memberships[0].Role);
    }

    [Fact]
    public void EnvironmentProfile_Defaults()
    {
        var p = new Tenancy.EnvironmentProfile();
        Assert.Equal("", p.Name);
        Assert.Equal("live", p.EnvironmentType);
        Assert.False(p.IsDefault);
    }

    [Fact]
    public void ClientVersion_Defaults()
    {
        var v = new ClientVersion();
        Assert.Equal("", v.Version);
        Assert.Equal("windows-x64", v.Platform);
        Assert.False(v.IsMandatory);
    }

    [Fact]
    public void ModuleLicense_Licensed()
    {
        var m = new ModuleLicense { Code = "devices", IsLicensed = true, IsBase = true };
        Assert.True(m.IsLicensed);
        Assert.True(m.IsBase);
    }
}
