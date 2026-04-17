using Central.Core.Models;

namespace Central.Tests.Models;

public class CrmModelsAdditionalTests
{
    // ─── CrmDeal ────────────────────────────────────────────────────────────

    [Fact]
    public void CrmDeal_IsClosedLost_WhenStageMatches()
    {
        Assert.True(new CrmDeal { Stage = "Closed Lost" }.IsClosedLost);
        Assert.False(new CrmDeal { Stage = "Qualification" }.IsClosedLost);
    }

    [Fact]
    public void CrmDeal_IsOpen_FalseWhenClosedWon()
    {
        Assert.False(new CrmDeal { Stage = "Closed Won" }.IsOpen);
    }

    [Fact]
    public void CrmDeal_IsOpen_FalseWhenClosedLost()
    {
        Assert.False(new CrmDeal { Stage = "Closed Lost" }.IsOpen);
    }

    [Fact]
    public void CrmDeal_WeightedValue_ZeroProbability()
    {
        var d = new CrmDeal { Value = 1000m, Probability = 0 };
        Assert.Equal(0m, d.WeightedValue);
    }

    [Fact]
    public void CrmDeal_WeightedValue_FullProbability()
    {
        var d = new CrmDeal { Value = 1000m, Probability = 100 };
        Assert.Equal(1000m, d.WeightedValue);
    }

    [Fact]
    public void CrmDeal_DaysToClose_ZeroWhenNoExpectedClose()
    {
        Assert.Equal(0, new CrmDeal { ExpectedClose = null }.DaysToClose);
    }

    // ─── CrmLead ────────────────────────────────────────────────────────────

    [Fact]
    public void CrmLead_FullName_TrimsWhenMissingLast()
    {
        Assert.Equal("Alice", new CrmLead { FirstName = "Alice", LastName = "" }.FullName);
    }

    [Fact]
    public void CrmLead_FullName_TrimsWhenMissingFirst()
    {
        Assert.Equal("Doe", new CrmLead { FirstName = "", LastName = "Doe" }.FullName);
    }

    [Fact]
    public void CrmLead_Temperature_BoundaryHot()
    {
        Assert.Equal("hot", new CrmLead { Score = 75 }.Temperature);
        Assert.Equal("warm", new CrmLead { Score = 74 }.Temperature);
    }

    // ─── CrmQuote ───────────────────────────────────────────────────────────

    [Fact]
    public void CrmQuote_IsExpired_FalseWhenNoValidUntil()
    {
        Assert.False(new CrmQuote { ValidUntil = null }.IsExpired);
    }

    [Fact]
    public void CrmQuote_IsExpired_FalseWhenValidInFuture()
    {
        Assert.False(new CrmQuote { ValidUntil = DateTime.UtcNow.AddDays(5) }.IsExpired);
    }

    // ─── AccountContactLink ─────────────────────────────────────────────────

    [Fact]
    public void AccountContactLink_Defaults()
    {
        var link = new AccountContactLink();
        Assert.Equal("user", link.RoleInAccount);
        Assert.False(link.IsPrimary);
    }

    // ─── Subscription ───────────────────────────────────────────────────────

    [Fact]
    public void Subscription_IsTrial_WhenStatusTrial()
    {
        Assert.True(new Subscription { Status = "trial" }.IsTrial);
        Assert.False(new Subscription { Status = "active" }.IsTrial);
    }

    // ─── Order ──────────────────────────────────────────────────────────────

    [Fact]
    public void Order_IsInvoiced_WhenInvoicedAtSet()
    {
        Assert.True(new Order { InvoicedAt = DateTime.UtcNow }.IsInvoiced);
        Assert.False(new Order { InvoicedAt = null }.IsInvoiced);
    }

    // ─── Contract ───────────────────────────────────────────────────────────

    [Fact]
    public void Contract_DaysToExpiry_NullWhenNoEndDate()
    {
        Assert.Null(new Contract { EndDate = null }.DaysToExpiry);
    }

    [Fact]
    public void Contract_IsSigned_TrueWhenSignedByNameSet()
    {
        Assert.True(new Contract { SignedByName = "Jane Doe" }.IsSigned);
    }

    [Fact]
    public void Contract_IsInRenewalWindow_FalseWhenExpired()
    {
        var c = new Contract
        {
            EndDate = DateTime.UtcNow.AddDays(-1),
            RenewalNoticeDays = 90
        };
        Assert.False(c.IsInRenewalWindow);
    }

    // ─── SubscriptionEvent ─────────────────────────────────────────────────

    [Fact]
    public void SubscriptionEvent_NeutralDelta_NotExpansionOrContraction()
    {
        var e = new SubscriptionEvent { MrrDelta = 0m };
        Assert.False(e.IsExpansion);
        Assert.False(e.IsContraction);
    }
}
