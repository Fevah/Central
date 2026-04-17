using Central.Core.Auth;

namespace Central.Tests.Auth;

/// <summary>Tests for new permission codes added in Phases 1-14.</summary>
public class PermissionCodePhase1To14Tests
{
    [Theory]
    [InlineData(P.CompaniesRead, "companies:read")]
    [InlineData(P.CompaniesWrite, "companies:write")]
    [InlineData(P.CompaniesDelete, "companies:delete")]
    [InlineData(P.ContactsRead, "contacts:read")]
    [InlineData(P.ContactsWrite, "contacts:write")]
    [InlineData(P.ContactsDelete, "contacts:delete")]
    [InlineData(P.ContactsExport, "contacts:export")]
    [InlineData(P.AdminTeams, "admin:teams")]
    [InlineData(P.AdminDepartments, "admin:departments")]
    [InlineData(P.ProfilesRead, "profiles:read")]
    [InlineData(P.ProfilesWrite, "profiles:write")]
    [InlineData(P.CrmRead, "crm:read")]
    [InlineData(P.CrmWrite, "crm:write")]
    [InlineData(P.CrmDelete, "crm:delete")]
    [InlineData(P.CrmAdmin, "crm:admin")]
    [InlineData(P.GlobalAdminRead, "global_admin:read")]
    [InlineData(P.GlobalAdminWrite, "global_admin:write")]
    public void PermissionCode_HasCorrectValue(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PermissionCodes_AllFollowNamespacedFormat()
    {
        // Every code is module:action OR module:subpath:action — no flat
        // strings, no empty segments. AI-era codes like "ai:providers:read"
        // introduced the three-part form; the rule is now "at least 2
        // colon-separated segments, none empty".
        var fields = typeof(P).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            Assert.Contains(":", value);
            var parts = value.Split(':');
            Assert.True(parts.Length >= 2, $"Code '{value}' must have at least one colon");
            foreach (var p in parts)
                Assert.False(string.IsNullOrEmpty(p), $"Code '{value}' has an empty segment");
        }
    }
}
