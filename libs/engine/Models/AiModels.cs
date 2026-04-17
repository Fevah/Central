namespace Central.Engine.Models;

// ─── Provider registry + tenant configuration ───────────────────────────────

public class AiProvider
{
    public int Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ProviderType { get; set; } = "cloud_api";
    public string BaseUrl { get; set; } = "";
    public string AuthType { get; set; } = "api_key";
    public bool PlatformKeyConfigured { get; set; }
    public bool SupportsChat { get; set; } = true;
    public bool SupportsEmbeddings { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsToolUse { get; set; }
    public bool SupportsStreaming { get; set; } = true;
    public int? RateLimitRpm { get; set; }
    public int? RateLimitTpm { get; set; }
    public string DocsUrl { get; set; } = "";
    public string IconUrl { get; set; } = "";
    public string BrandColor { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }

    public bool IsLocal => ProviderType is "self_hosted" or "local";
    public bool IsCloud => ProviderType == "cloud_api";
}

public class AiModel
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ModelCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ModelFamily { get; set; } = "";
    public int? ContextWindow { get; set; }
    public int? MaxOutputTokens { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsToolUse { get; set; }
    public decimal? InputPricePerM { get; set; }
    public decimal? OutputPricePerM { get; set; }
    public string Tier { get; set; } = "";
    public bool IsRecommended { get; set; }
    public bool IsDeprecated { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TenantAiProvider
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public int ProviderId { get; set; }
    public string ProviderCode { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool IsDefaultForTenant { get; set; }
    public bool UsePlatformKey { get; set; } = true;
    public bool HasByok { get; set; }          // derived — true if api_key_enc populated
    public string ApiKeyLabel { get; set; } = "";
    public string OrgId { get; set; } = "";
    public string DefaultModelCode { get; set; } = "";
    public long? MonthlyTokenLimit { get; set; }
    public decimal? MonthlyCostLimit { get; set; }
    public long CurrentMonthTokens { get; set; }
    public decimal CurrentMonthCost { get; set; }
    public DateTime? LastUsedAt { get; set; }

    public decimal TokenUsagePct =>
        MonthlyTokenLimit.GetValueOrDefault() > 0
            ? (decimal)CurrentMonthTokens / MonthlyTokenLimit.Value * 100m
            : 0;
    public decimal CostUsagePct =>
        MonthlyCostLimit.GetValueOrDefault() > 0
            ? CurrentMonthCost / MonthlyCostLimit.Value * 100m
            : 0;
    public bool IsOverBudget => CostUsagePct >= 100m || TokenUsagePct >= 100m;
}

public class TenantAiFeatureConfig
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string FeatureCode { get; set; } = "";  // assistant, lead_scoring, summarize, etc.
    public int? ProviderId { get; set; }
    public string ModelCode { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public string CustomSystemPrompt { get; set; } = "";
    public decimal? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}

/// <summary>Result of resolving which provider + key to use for a tenant + feature.</summary>
public class AiProviderResolution
{
    public int ProviderId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ModelCode { get; set; } = "";
    public string KeySource { get; set; } = "";     // tenant_byok, platform, none
    public bool HasByok { get; set; }
    public long QuotaRemaining { get; set; }
    public decimal CostRemaining { get; set; }

    public bool IsAvailable => KeySource != "none" && ProviderId > 0;
    public bool IsWithinQuota => QuotaRemaining > 0 && CostRemaining > 0;
}

public class AiUsageEntry
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public int? UserId { get; set; }
    public int ProviderId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ModelCode { get; set; } = "";
    public string FeatureCode { get; set; } = "";
    public string KeySource { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
    public decimal CostUsd { get; set; }
    public int? LatencyMs { get; set; }
    public bool Success { get; set; } = true;
    public string ErrorCode { get; set; } = "";
    public DateTime CalledAt { get; set; }
}

// ─── ML Scoring ──────────────────────────────────────────────────────────────

public class MlModel
{
    public int Id { get; set; }
    public Guid? TenantId { get; set; }       // NULL = platform-shared
    public string ModelName { get; set; } = "";
    public string ModelKind { get; set; } = ""; // lead_scoring, opp_scoring, churn, ltv, next_action, duplicate_match
    public string Framework { get; set; } = "logreg";
    public string TargetLabel { get; set; } = "";
    public string Status { get; set; } = "draft";
    public int Version { get; set; } = 1;
    public bool IsChampion { get; set; }
    public int? TrainSamples { get; set; }
    public decimal? TrainAuc { get; set; }
    public decimal? TrainAccuracy { get; set; }
    public decimal? TrainF1 { get; set; }
    public DateTime? TrainedAt { get; set; }
    public string ArtifactUri { get; set; } = "";
    public string ArtifactFormat { get; set; } = "";
    public string Notes { get; set; } = "";

    public bool IsActive => Status == "active";
}

public class ModelScore
{
    public long Id { get; set; }
    public int ModelId { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public decimal Score { get; set; }
    public decimal? Confidence { get; set; }
    public string Explanation { get; set; } = "";
    public DateTime ScoredAt { get; set; }
    public string ActualOutcome { get; set; } = "";

    public string Tier => Score switch
    {
        >= 75 => "hot",
        >= 40 => "warm",
        _ => "cold"
    };
}

public class NextBestAction
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string ActionCode { get; set; } = "";
    public string ActionText { get; set; } = "";
    public string Rationale { get; set; } = "";
    public decimal? ExpectedValue { get; set; }
    public decimal? Confidence { get; set; }
    public int Priority { get; set; } = 100;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public DateTime? ActedOnAt { get; set; }
    public string Outcome { get; set; } = "";

    public bool IsPending => AcceptedAt == null && DismissedAt == null && ActedOnAt == null;
}

// ─── AI Assistant ───────────────────────────────────────────────────────────

public class AiConversation
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Title { get; set; } = "";
    public string ContextEntityType { get; set; } = "";
    public int? ContextEntityId { get; set; }
    public int? ProviderId { get; set; }
    public string ProviderCode { get; set; } = "";
    public string ModelCode { get; set; } = "";
    public string Status { get; set; } = "active";
    public int MessageCount { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

public class AiMessage
{
    public long Id { get; set; }
    public int ConversationId { get; set; }
    public string Role { get; set; } = "user";   // system, user, assistant, tool
    public string Content { get; set; } = "";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? LatencyMs { get; set; }
    public bool ModerationFlagged { get; set; }
    public int? Thumbs { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PromptTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string UserPromptTemplate { get; set; } = "";
    public string[] Variables { get; set; } = [];
    public string SuggestedModel { get; set; } = "";
    public decimal? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; } = true;
    public int UseCount { get; set; }
}

// ─── Duplicates + Enrichment ────────────────────────────────────────────────

public class DuplicateCandidate
{
    public long Id { get; set; }
    public int? RuleId { get; set; }
    public string EntityType { get; set; } = "";
    public int RecordAId { get; set; }
    public int RecordBId { get; set; }
    public decimal SimilarityScore { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? ReviewedAt { get; set; }
    public int? MergedIntoId { get; set; }
}

public class EnrichmentJob
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public int? ProviderId { get; set; }
    public string Status { get; set; } = "queued";
    public string MatchField { get; set; } = "";
    public string MatchValue { get; set; } = "";
    public decimal? MatchConfidence { get; set; }
    public string[] FieldsUpdated { get; set; } = [];
    public decimal? CostUsd { get; set; }
    public string ErrorMessage { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

// ─── Churn + LTV + Calls ────────────────────────────────────────────────────

public class ChurnRisk
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public decimal RiskScore { get; set; }
    public string RiskTier { get; set; } = "";
    public string[] ContributingFactors { get; set; } = [];
    public string[] RecommendedActions { get; set; } = [];
    public int? LastActivityDaysAgo { get; set; }
    public int? OpenTicketCount { get; set; }
    public int? ContractRenewsInDays { get; set; }
    public decimal? MrrAtRisk { get; set; }
    public DateTime ScoredAt { get; set; }
    public string ActualOutcome { get; set; } = "";

    public bool IsHighRisk => RiskTier is "high" or "critical";
}

public class AccountLtv
{
    public int AccountId { get; set; }
    public decimal HistoricalRevenue { get; set; }
    public DateTime? FirstDealAt { get; set; }
    public int TotalDeals { get; set; }
    public decimal ActiveMrr { get; set; }
    public decimal ActiveArr { get; set; }
    public decimal? ProjectedLtv { get; set; }
    public int? ProjectedRemainingMonths { get; set; }
    public decimal? ProjectionConfidence { get; set; }
    public DateTime CalculatedAt { get; set; }
}

public class CallRecording
{
    public long Id { get; set; }
    public long? ActivityId { get; set; }
    public string ExternalId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string RecordingUrl { get; set; } = "";
    public int? DurationSeconds { get; set; }
    public DateTime? StartedAt { get; set; }
    public int? LinkedContactId { get; set; }
    public int? LinkedDealId { get; set; }
    public int? LinkedAccountId { get; set; }
    public string TranscriptStatus { get; set; } = "pending";
    public string TranscriptText { get; set; } = "";
    public string Summary { get; set; } = "";
    public string[] ActionItems { get; set; } = [];
    public string[] TopicsDiscussed { get; set; } = [];
    public decimal? OverallSentiment { get; set; }
    public int? LongestMonologueSeconds { get; set; }
    public int? QuestionCount { get; set; }
    public decimal? ProcessingCostUsd { get; set; }
    public DateTime CreatedAt { get; set; }

    public string SentimentLabel => OverallSentiment switch
    {
        > 0.3m  => "positive",
        < -0.3m => "negative",
        _       => "neutral"
    };
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds ?? 0);
}
