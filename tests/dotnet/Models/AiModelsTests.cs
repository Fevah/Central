using Central.Engine.Models;

namespace Central.Tests.Models;

public class AiModelsTests
{
    // ─── AiProvider ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("self_hosted", true,  false)]
    [InlineData("local",       true,  false)]
    [InlineData("cloud_api",   false, true)]
    [InlineData("other",       false, false)]
    public void AiProvider_LocalCloudFlags_MapFromProviderType(string type, bool expLocal, bool expCloud)
    {
        var p = new AiProvider { ProviderType = type };
        Assert.Equal(expLocal, p.IsLocal);
        Assert.Equal(expCloud, p.IsCloud);
    }

    // ─── TenantAiProvider quota math ───────────────────────────────────────

    [Fact]
    public void TenantAiProvider_TokenUsagePct_Calculated()
    {
        var t = new TenantAiProvider { MonthlyTokenLimit = 1_000_000, CurrentMonthTokens = 250_000 };
        Assert.Equal(25m, t.TokenUsagePct);
    }

    [Fact]
    public void TenantAiProvider_TokenUsagePct_ZeroWhenNoLimit()
    {
        var t = new TenantAiProvider { MonthlyTokenLimit = null, CurrentMonthTokens = 500_000 };
        Assert.Equal(0m, t.TokenUsagePct);
    }

    [Fact]
    public void TenantAiProvider_TokenUsagePct_ZeroWhenLimitZero()
    {
        var t = new TenantAiProvider { MonthlyTokenLimit = 0, CurrentMonthTokens = 100 };
        Assert.Equal(0m, t.TokenUsagePct);
    }

    [Fact]
    public void TenantAiProvider_CostUsagePct_Calculated()
    {
        var t = new TenantAiProvider { MonthlyCostLimit = 200m, CurrentMonthCost = 50m };
        Assert.Equal(25m, t.CostUsagePct);
    }

    [Fact]
    public void TenantAiProvider_CostUsagePct_ZeroWhenNoLimit()
    {
        var t = new TenantAiProvider { MonthlyCostLimit = null, CurrentMonthCost = 100m };
        Assert.Equal(0m, t.CostUsagePct);
    }

    [Fact]
    public void TenantAiProvider_IsOverBudget_TrueWhenCostAtLimit()
    {
        var t = new TenantAiProvider { MonthlyCostLimit = 100m, CurrentMonthCost = 100m };
        Assert.True(t.IsOverBudget);
    }

    [Fact]
    public void TenantAiProvider_IsOverBudget_TrueWhenTokensAtLimit()
    {
        var t = new TenantAiProvider { MonthlyTokenLimit = 1000, CurrentMonthTokens = 1000 };
        Assert.True(t.IsOverBudget);
    }

    [Fact]
    public void TenantAiProvider_IsOverBudget_FalseWhenUnderBoth()
    {
        var t = new TenantAiProvider
        {
            MonthlyTokenLimit = 1000, CurrentMonthTokens = 100,
            MonthlyCostLimit = 100m,  CurrentMonthCost = 10m
        };
        Assert.False(t.IsOverBudget);
    }

    [Fact]
    public void TenantAiProvider_IsOverBudget_FalseWhenNoLimitsSet()
    {
        var t = new TenantAiProvider { CurrentMonthTokens = 999_999_999, CurrentMonthCost = 999_999m };
        Assert.False(t.IsOverBudget);
    }

    // ─── AiProviderResolution ──────────────────────────────────────────────

    [Fact]
    public void AiProviderResolution_IsAvailable_TrueWhenResolvedWithKey()
    {
        var r = new AiProviderResolution { ProviderId = 1, KeySource = "tenant_byok" };
        Assert.True(r.IsAvailable);
    }

    [Fact]
    public void AiProviderResolution_IsAvailable_FalseWhenKeySourceNone()
    {
        var r = new AiProviderResolution { ProviderId = 1, KeySource = "none" };
        Assert.False(r.IsAvailable);
    }

    [Fact]
    public void AiProviderResolution_IsAvailable_FalseWhenProviderIdZero()
    {
        var r = new AiProviderResolution { ProviderId = 0, KeySource = "platform" };
        Assert.False(r.IsAvailable);
    }

    [Theory]
    [InlineData(1000L, 10.0,  true)]
    [InlineData(0L,    10.0,  false)]
    [InlineData(1000L, 0.0,   false)]
    [InlineData(-1L,   10.0,  false)]
    public void AiProviderResolution_IsWithinQuota_ReflectsRemaining(long tokens, double cost, bool expected)
    {
        var r = new AiProviderResolution { QuotaRemaining = tokens, CostRemaining = (decimal)cost };
        Assert.Equal(expected, r.IsWithinQuota);
    }

    // ─── AiUsageEntry ──────────────────────────────────────────────────────

    [Fact]
    public void AiUsageEntry_TotalTokens_SumsInputAndOutput()
    {
        var e = new AiUsageEntry { InputTokens = 123, OutputTokens = 456 };
        Assert.Equal(579, e.TotalTokens);
    }

    // ─── MlModel ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("active", true)]
    [InlineData("draft", false)]
    [InlineData("training", false)]
    [InlineData("archived", false)]
    public void MlModel_IsActive_MatchesStatus(string status, bool expected)
    {
        Assert.Equal(expected, new MlModel { Status = status }.IsActive);
    }

    // ─── ModelScore tier thresholds ────────────────────────────────────────

    [Theory]
    [InlineData(90.0,  "hot")]
    [InlineData(75.0,  "hot")]
    [InlineData(74.99, "warm")]
    [InlineData(50.0,  "warm")]
    [InlineData(40.0,  "warm")]
    [InlineData(39.99, "cold")]
    [InlineData(0.0,   "cold")]
    public void ModelScore_Tier_MapsByScoreThreshold(double score, string expected)
    {
        Assert.Equal(expected, new ModelScore { Score = (decimal)score }.Tier);
    }

    // ─── NextBestAction ────────────────────────────────────────────────────

    [Fact]
    public void NextBestAction_IsPending_WhenNoTerminalTimestamps()
    {
        Assert.True(new NextBestAction().IsPending);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void NextBestAction_IsPending_FalseOnceActedOn(bool accepted, bool dismissed, bool acted)
    {
        var n = new NextBestAction
        {
            AcceptedAt  = accepted  ? DateTime.UtcNow : null,
            DismissedAt = dismissed ? DateTime.UtcNow : null,
            ActedOnAt   = acted     ? DateTime.UtcNow : null
        };
        Assert.False(n.IsPending);
    }

    // ─── ChurnRisk ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("critical", true)]
    [InlineData("high",     true)]
    [InlineData("medium",   false)]
    [InlineData("low",      false)]
    public void ChurnRisk_IsHighRisk_MapsFromTier(string tier, bool expected)
    {
        Assert.Equal(expected, new ChurnRisk { RiskTier = tier }.IsHighRisk);
    }

    // ─── CallRecording ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.9,   "positive")]
    [InlineData(0.31,  "positive")]
    [InlineData(0.30,  "neutral")]
    [InlineData(0.0,   "neutral")]
    [InlineData(-0.30, "neutral")]
    [InlineData(-0.31, "negative")]
    [InlineData(-0.9,  "negative")]
    public void CallRecording_SentimentLabel_MapsByThreshold(double sentiment, string expected)
    {
        var c = new CallRecording { OverallSentiment = (decimal)sentiment };
        Assert.Equal(expected, c.SentimentLabel);
    }

    [Fact]
    public void CallRecording_SentimentLabel_NeutralWhenNull()
    {
        Assert.Equal("neutral", new CallRecording { OverallSentiment = null }.SentimentLabel);
    }

    [Fact]
    public void CallRecording_Duration_BuiltFromSeconds()
    {
        var c = new CallRecording { DurationSeconds = 125 };
        Assert.Equal(TimeSpan.FromSeconds(125), c.Duration);
    }

    [Fact]
    public void CallRecording_Duration_ZeroWhenNull()
    {
        Assert.Equal(TimeSpan.Zero, new CallRecording { DurationSeconds = null }.Duration);
    }
}
