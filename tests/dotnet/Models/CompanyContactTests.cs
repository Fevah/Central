using Central.Core.Models;

namespace Central.Tests.Models;

/// <summary>Tests for Phase 1-5 foundation entity models.</summary>
public class CompanyContactTests
{
    [Fact]
    public void CompanyRecord_DefaultState()
    {
        var c = new CompanyRecord();
        Assert.Equal(0, c.Id);
        Assert.Equal("", c.Name);
        Assert.True(c.IsActive);
        Assert.Null(c.ParentId);
    }

    [Fact]
    public void CompanyRecord_PropertyChanged_Fires()
    {
        var c = new CompanyRecord();
        string? changed = null;
        c.PropertyChanged += (_, e) => changed = e.PropertyName;
        c.Name = "Acme Corp";
        Assert.Equal(nameof(CompanyRecord.Name), changed);
    }

    [Fact]
    public void ContactRecord_FullName_CombinesFirstLast()
    {
        var c = new ContactRecord { FirstName = "John", LastName = "Smith" };
        Assert.Equal("John Smith", c.FullName);
    }

    [Fact]
    public void ContactRecord_FullName_TrimsWhitespace()
    {
        var c = new ContactRecord { FirstName = "", LastName = "Smith" };
        Assert.Equal("Smith", c.FullName);
    }

    [Fact]
    public void ContactRecord_DefaultType_IsCustomer()
    {
        var c = new ContactRecord();
        Assert.Equal("customer", c.ContactType);
        Assert.Equal("active", c.Status);
    }

    [Fact]
    public void DepartmentRecord_DefaultState()
    {
        var d = new DepartmentRecord();
        Assert.True(d.IsActive);
        Assert.Null(d.ParentId);
        Assert.Equal(0, d.MemberCount);
    }

    [Fact]
    public void TeamRecord_DefaultState()
    {
        var t = new TeamRecord();
        Assert.True(t.IsActive);
        Assert.Null(t.DepartmentId);
        Assert.Equal(0, t.MemberCount);
    }

    [Fact]
    public void AddressRecord_OneLine_FormatsCorrectly()
    {
        var a = new AddressRecord
        {
            Line1 = "123 Main St",
            City = "London",
            PostalCode = "EC1A 1BB",
            CountryCode = "GB"
        };
        Assert.Equal("123 Main St, London, EC1A 1BB, GB", a.OneLine);
    }

    [Fact]
    public void AddressRecord_OneLine_SkipsEmpty()
    {
        var a = new AddressRecord { Line1 = "123 Main St", City = "London", CountryCode = "GB" };
        Assert.Equal("123 Main St, London, GB", a.OneLine);
    }

    [Fact]
    public void UserProfile_Defaults()
    {
        var p = new UserProfile();
        Assert.Equal("UTC", p.Timezone);
        Assert.Equal("en-GB", p.Locale);
        Assert.Equal("dd/MM/yyyy", p.DateFormat);
        Assert.Equal("HH:mm", p.TimeFormat);
    }

    [Fact]
    public void InvitationRecord_Status_Pending()
    {
        var i = new InvitationRecord { ExpiresAt = DateTime.UtcNow.AddDays(3) };
        Assert.Equal("Pending", i.Status);
        Assert.False(i.IsExpired);
        Assert.False(i.IsAccepted);
    }

    [Fact]
    public void InvitationRecord_Status_Expired()
    {
        var i = new InvitationRecord { ExpiresAt = DateTime.UtcNow.AddDays(-1) };
        Assert.Equal("Expired", i.Status);
        Assert.True(i.IsExpired);
    }

    [Fact]
    public void InvitationRecord_Status_Accepted()
    {
        var i = new InvitationRecord
        {
            ExpiresAt = DateTime.UtcNow.AddDays(3),
            AcceptedAt = DateTime.UtcNow
        };
        Assert.Equal("Accepted", i.Status);
        Assert.True(i.IsAccepted);
    }

    [Fact]
    public void ContactCommunication_DefaultChannel()
    {
        var cc = new ContactCommunication();
        Assert.Equal("", cc.Channel);
        Assert.Equal("", cc.Direction);
    }

    [Fact]
    public void BillingAccount_DefaultCurrency_GBP()
    {
        var ba = new BillingAccount();
        Assert.Equal("GBP", ba.Currency);
        Assert.False(ba.TaxExempt);
    }

    [Fact]
    public void UsageMetric_DefaultState()
    {
        var m = new UsageMetric();
        Assert.Equal("", m.MetricType);
        Assert.Equal(0m, m.Value);
    }
}
