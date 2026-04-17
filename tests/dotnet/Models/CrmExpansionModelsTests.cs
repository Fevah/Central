using Central.Core.Models;

namespace Central.Tests.Models;

public class CrmExpansionModelsTests
{
    // ─── Stage 1: Marketing ──────────────────────────────────────────────────

    [Fact]
    public void Campaign_Roi_Calculated()
    {
        var c = new Campaign { Budget = 10000m, ActualCost = 8000m, ExpectedRevenue = 30000m };
        Assert.Equal(220m, c.Roi);
    }

    [Fact]
    public void Campaign_Roi_NullWhenNoBudget()
    {
        var c = new Campaign { Budget = null, ExpectedRevenue = 100m };
        Assert.Null(c.Roi);
    }

    [Fact]
    public void Segment_IsDynamic_DetectedCorrectly()
    {
        Assert.True(new Segment { SegmentType = "dynamic" }.IsDynamic);
        Assert.False(new Segment { SegmentType = "static" }.IsDynamic);
    }

    [Fact]
    public void EmailSequence_CompletionRate_Calculated()
    {
        var s = new EmailSequence { TotalEnrollments = 100, TotalCompletions = 35 };
        Assert.Equal(35m, s.CompletionRate);
    }

    [Fact]
    public void EmailSequence_CompletionRate_ZeroWhenNoEnrollments()
    {
        var s = new EmailSequence { TotalEnrollments = 0 };
        Assert.Equal(0m, s.CompletionRate);
    }

    [Fact]
    public void LandingPage_ConversionRate_Calculated()
    {
        var p = new LandingPage { ViewCount = 1000, SubmissionCount = 42 };
        Assert.Equal(4.2m, p.ConversionRate);
    }

    // ─── Stage 2: Sales Ops ──────────────────────────────────────────────────

    [Fact]
    public void Quota_AttainmentPct_Calculated()
    {
        var q = new Quota { TargetAmount = 100000m, AchievedAmount = 75000m };
        Assert.Equal(75m, q.AttainmentPct);
    }

    [Fact]
    public void Quota_AttainmentPct_ZeroWhenNoTarget()
    {
        var q = new Quota { TargetAmount = 0, AchievedAmount = 50000m };
        Assert.Equal(0m, q.AttainmentPct);
    }

    [Fact]
    public void DealInsight_IsCritical_DetectedCorrectly()
    {
        Assert.True(new DealInsight { Severity = "critical" }.IsCritical);
        Assert.False(new DealInsight { Severity = "warn" }.IsCritical);
    }

    [Theory]
    [InlineData(4.0, "excellent")]
    [InlineData(3.5, "healthy")]
    [InlineData(2.5, "warning")]
    [InlineData(1.0, "critical")]
    public void PipelineHealth_HealthRating_MapsCorrectly(double coverageRatio, string expected)
    {
        var p = new PipelineHealth { CoverageRatio = (decimal)coverageRatio };
        Assert.Equal(expected, p.HealthRating);
    }

    // ─── Stage 3: CPQ / Contracts / Revenue ─────────────────────────────────

    [Fact]
    public void ApprovalRequest_IsPending_WhenStatusPending()
    {
        Assert.True(new ApprovalRequest { Status = "pending" }.IsPending);
        Assert.False(new ApprovalRequest { Status = "approved" }.IsPending);
    }

    [Fact]
    public void ApprovalRequest_IsOverdue_WhenExpiredAndPending()
    {
        var r = new ApprovalRequest { Status = "pending", ExpiresAt = DateTime.UtcNow.AddHours(-1) };
        Assert.True(r.IsOverdue);
    }

    [Fact]
    public void ApprovalRequest_IsOverdue_FalseWhenResolved()
    {
        var r = new ApprovalRequest { Status = "approved", ExpiresAt = DateTime.UtcNow.AddHours(-1) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void Contract_IsSigned_WhenSignedAtSet()
    {
        Assert.True(new Contract { SignedAt = DateTime.UtcNow }.IsSigned);
        Assert.False(new Contract { }.IsSigned);
    }

    [Fact]
    public void Contract_IsInRenewalWindow_DetectedCorrectly()
    {
        var c = new Contract
        {
            EndDate = DateTime.UtcNow.AddDays(60),
            RenewalNoticeDays = 90
        };
        Assert.True(c.IsInRenewalWindow);
    }

    [Fact]
    public void Contract_IsInRenewalWindow_FalseWhenFarOut()
    {
        var c = new Contract
        {
            EndDate = DateTime.UtcNow.AddDays(365),
            RenewalNoticeDays = 90
        };
        Assert.False(c.IsInRenewalWindow);
    }

    [Fact]
    public void Contract_DaysToExpiry_Calculated()
    {
        var c = new Contract { EndDate = DateTime.UtcNow.AddDays(45) };
        Assert.InRange(c.DaysToExpiry ?? 0, 44, 46);
    }

    [Fact]
    public void ContractMilestone_IsOverdue_WhenPastDue()
    {
        var m = new ContractMilestone { Status = "pending", DueDate = DateTime.UtcNow.Date.AddDays(-1) };
        Assert.True(m.IsOverdue);
    }

    [Fact]
    public void ContractMilestone_IsOverdue_FalseWhenCompleted()
    {
        var m = new ContractMilestone { Status = "completed", DueDate = DateTime.UtcNow.Date.AddDays(-1) };
        Assert.False(m.IsOverdue);
    }

    [Fact]
    public void Subscription_IsActive_WhenStatusActive()
    {
        Assert.True(new Subscription { Status = "active" }.IsActive);
        Assert.False(new Subscription { Status = "trial" }.IsActive);
    }

    [Fact]
    public void Subscription_IsChurned_WhenCancelled()
    {
        Assert.True(new Subscription { Status = "cancelled" }.IsChurned);
    }

    [Fact]
    public void SubscriptionEvent_IsExpansion_WhenMrrIncreases()
    {
        var e = new SubscriptionEvent { MrrDelta = 100m };
        Assert.True(e.IsExpansion);
        Assert.False(e.IsContraction);
    }

    [Fact]
    public void SubscriptionEvent_IsContraction_WhenMrrDecreases()
    {
        var e = new SubscriptionEvent { MrrDelta = -50m };
        Assert.True(e.IsContraction);
        Assert.False(e.IsExpansion);
    }

    [Fact]
    public void MrrDashboard_ChurnRatePct_Calculated()
    {
        var d = new MrrDashboard { ActiveSubscriptions = 95, ChurnedLast30d = 5 };
        Assert.Equal(5m, d.ChurnRatePct);
    }

    [Fact]
    public void MrrDashboard_ChurnRatePct_ZeroWhenNoChurn()
    {
        var d = new MrrDashboard { ActiveSubscriptions = 100, ChurnedLast30d = 0 };
        Assert.Equal(0m, d.ChurnRatePct);
    }

    [Fact]
    public void Order_IsFulfilled_WhenFulfilledAtSet()
    {
        Assert.True(new Order { FulfilledAt = DateTime.UtcNow }.IsFulfilled);
        Assert.False(new Order { }.IsFulfilled);
    }
}

public class CrmExpansionPermissionTests
{
    [Theory]
    [InlineData(Central.Core.Auth.P.MarketingRead, "marketing:read")]
    [InlineData(Central.Core.Auth.P.MarketingCampaigns, "marketing:campaigns")]
    [InlineData(Central.Core.Auth.P.MarketingSegments, "marketing:segments")]
    [InlineData(Central.Core.Auth.P.SalesOpsTerritories, "salesops:territories")]
    [InlineData(Central.Core.Auth.P.SalesOpsQuotas, "salesops:quotas")]
    [InlineData(Central.Core.Auth.P.SalesOpsCommissions, "salesops:commissions")]
    [InlineData(Central.Core.Auth.P.CpqBundles, "cpq:bundles")]
    [InlineData(Central.Core.Auth.P.CpqDiscountApproval, "cpq:discount_approval")]
    [InlineData(Central.Core.Auth.P.ContractsRead, "contracts:read")]
    [InlineData(Central.Core.Auth.P.SubscriptionsWrite, "subscriptions:write")]
    [InlineData(Central.Core.Auth.P.RevenueRead, "revenue:read")]
    [InlineData(Central.Core.Auth.P.OrdersWrite, "orders:write")]
    [InlineData(Central.Core.Auth.P.ApprovalsAct, "approvals:act")]
    public void PermissionCode_HasCorrectValue(string actual, string expected)
    {
        Assert.Equal(expected, actual);
    }
}
