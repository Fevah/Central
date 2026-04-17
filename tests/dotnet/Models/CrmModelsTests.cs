using Central.Core.Models;

namespace Central.Tests.Models;

public class CrmModelsTests
{
    // ── CrmAccount ──

    [Fact]
    public void CrmAccount_DefaultState()
    {
        var a = new CrmAccount();
        Assert.Equal("customer", a.AccountType);
        Assert.Equal("prospecting", a.Stage);
        Assert.True(a.IsActive);
    }

    [Fact]
    public void CrmAccount_IsHot_WhenRatingHot()
    {
        var a = new CrmAccount { Rating = "hot" };
        Assert.True(a.IsHot);
    }

    [Fact]
    public void CrmAccount_IsCustomer_WhenCustomerType()
    {
        var a = new CrmAccount { AccountType = "customer" };
        Assert.True(a.IsCustomer);
        Assert.False(new CrmAccount { AccountType = "prospect" }.IsCustomer);
    }

    [Fact]
    public void CrmAccount_PropertyChanged_Fires()
    {
        var a = new CrmAccount();
        string? changed = null;
        a.PropertyChanged += (_, e) => changed = e.PropertyName;
        a.Name = "Acme Corp";
        Assert.Equal(nameof(CrmAccount.Name), changed);
    }

    // ── CrmDeal ──

    [Fact]
    public void CrmDeal_WeightedValue_Calculated()
    {
        var d = new CrmDeal { Value = 10000m, Probability = 60 };
        Assert.Equal(6000m, d.WeightedValue);
    }

    [Fact]
    public void CrmDeal_WeightedValue_ZeroWhenNull()
    {
        var d = new CrmDeal { Value = null, Probability = 50 };
        Assert.Equal(0m, d.WeightedValue);
    }

    [Fact]
    public void CrmDeal_IsOpen_WhenNotClosed()
    {
        var d = new CrmDeal { Stage = "Proposal" };
        Assert.True(d.IsOpen);
        Assert.False(d.IsClosedWon);
        Assert.False(d.IsClosedLost);
    }

    [Fact]
    public void CrmDeal_IsClosedWon_WhenStageMatches()
    {
        var d = new CrmDeal { Stage = "Closed Won" };
        Assert.True(d.IsClosedWon);
        Assert.False(d.IsOpen);
    }

    [Fact]
    public void CrmDeal_DaysToClose_Calculated()
    {
        var d = new CrmDeal { ExpectedClose = DateTime.UtcNow.AddDays(15) };
        Assert.InRange(d.DaysToClose, 14, 16);
    }

    [Fact]
    public void CrmDeal_PropertyChanged_UpdatesWeightedValue()
    {
        var d = new CrmDeal { Value = 1000m, Probability = 50 };
        var changed = new List<string>();
        d.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");
        d.Value = 2000m;
        Assert.Contains(nameof(CrmDeal.Value), changed);
        Assert.Contains(nameof(CrmDeal.WeightedValue), changed);
    }

    // ── CrmLead ──

    [Fact]
    public void CrmLead_FullName_Combined()
    {
        var l = new CrmLead { FirstName = "Jane", LastName = "Doe" };
        Assert.Equal("Jane Doe", l.FullName);
    }

    [Theory]
    [InlineData(10, "cold")]
    [InlineData(39, "cold")]
    [InlineData(40, "warm")]
    [InlineData(74, "warm")]
    [InlineData(75, "hot")]
    [InlineData(100, "hot")]
    public void CrmLead_Temperature_MapsCorrectly(int score, string expected)
    {
        var l = new CrmLead { Score = score };
        Assert.Equal(expected, l.Temperature);
    }

    [Fact]
    public void CrmLead_IsConverted_WhenConvertedAtSet()
    {
        var l = new CrmLead { ConvertedAt = DateTime.UtcNow };
        Assert.True(l.IsConverted);
    }

    [Fact]
    public void CrmLead_DefaultStatus_New()
    {
        var l = new CrmLead();
        Assert.Equal("new", l.Status);
        Assert.Equal(0, l.Score);
    }

    // ── CrmActivity ──

    [Fact]
    public void CrmActivity_IsOverdue_WhenDuePast()
    {
        var a = new CrmActivity
        {
            DueAt = DateTime.UtcNow.AddHours(-1),
            IsCompleted = false
        };
        Assert.True(a.IsOverdue);
    }

    [Fact]
    public void CrmActivity_IsOverdue_FalseWhenCompleted()
    {
        var a = new CrmActivity
        {
            DueAt = DateTime.UtcNow.AddHours(-1),
            IsCompleted = true
        };
        Assert.False(a.IsOverdue);
    }

    [Fact]
    public void CrmActivity_IsOverdue_FalseWhenDueFuture()
    {
        var a = new CrmActivity
        {
            DueAt = DateTime.UtcNow.AddHours(1),
            IsCompleted = false
        };
        Assert.False(a.IsOverdue);
    }

    // ── CrmProduct ──

    [Fact]
    public void CrmProduct_MarginPct_Calculated()
    {
        var p = new CrmProduct { UnitPrice = 100m, CostPrice = 40m };
        Assert.Equal(60m, p.MarginPct);
    }

    [Fact]
    public void CrmProduct_MarginPct_NullWhenNoCost()
    {
        var p = new CrmProduct { UnitPrice = 100m, CostPrice = null };
        Assert.Null(p.MarginPct);
    }

    [Fact]
    public void CrmProduct_MarginPct_NullWhenZeroPrice()
    {
        var p = new CrmProduct { UnitPrice = 0m, CostPrice = 10m };
        Assert.Null(p.MarginPct);
    }

    // ── CrmQuote ──

    [Fact]
    public void CrmQuote_IsAccepted_WhenStatusAccepted()
    {
        var q = new CrmQuote { Status = "accepted" };
        Assert.True(q.IsAccepted);
    }

    [Fact]
    public void CrmQuote_IsExpired_WhenValidUntilPastAndNotAccepted()
    {
        var q = new CrmQuote { ValidUntil = DateTime.UtcNow.AddDays(-1), Status = "sent" };
        Assert.True(q.IsExpired);
    }

    [Fact]
    public void CrmQuote_IsExpired_FalseWhenAccepted()
    {
        var q = new CrmQuote { ValidUntil = DateTime.UtcNow.AddDays(-1), Status = "accepted" };
        Assert.False(q.IsExpired);
    }

    [Fact]
    public void CrmQuote_DefaultState()
    {
        var q = new CrmQuote();
        Assert.Equal("draft", q.Status);
        Assert.Equal("GBP", q.Currency);
        Assert.Equal(1, q.Version);
    }

    // ── DealStage ──

    [Fact]
    public void DealStage_DefaultState()
    {
        var s = new DealStage();
        Assert.True(s.IsActive);
        Assert.False(s.IsWon);
        Assert.False(s.IsLost);
        Assert.Equal(50, s.Probability);
    }
}
