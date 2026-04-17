namespace Central.Engine.Models;

// ─── Stage 1: Marketing Automation ───────────────────────────────────────────

public class Campaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string CampaignType { get; set; } = "email";
    public string Status { get; set; } = "planning";
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = "";
    public int? ParentId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public decimal ActualCost { get; set; }
    public decimal? ExpectedRevenue { get; set; }
    public int? ExpectedResponses { get; set; }
    public string SourceCode { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public decimal? Roi => Budget.HasValue && Budget > 0 && ExpectedRevenue.HasValue
        ? (ExpectedRevenue.Value - ActualCost) / Budget.Value * 100m : null;
}

public class CampaignMember
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public string MemberType { get; set; } = "";  // lead, contact, account
    public int MemberId { get; set; }
    public string Status { get; set; } = "sent";
    public string ResponseType { get; set; } = "";
    public DateTime AddedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class Segment
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string SegmentType { get; set; } = "static";  // static, dynamic
    public string MemberType { get; set; } = "contact";
    public string RuleExpression { get; set; } = "";
    public int CachedCount { get; set; }
    public DateTime? LastEvaluatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsDynamic => SegmentType == "dynamic";
}

public class EmailSequence
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TriggerEvent { get; set; } = "manual";
    public bool IsActive { get; set; }
    public bool StopOnReply { get; set; } = true;
    public bool StopOnUnsubscribe { get; set; } = true;
    public bool StopOnMeeting { get; set; } = true;
    public int TotalEnrollments { get; set; }
    public int TotalCompletions { get; set; }
    public int TotalReplies { get; set; }

    public decimal CompletionRate => TotalEnrollments > 0
        ? (decimal)TotalCompletions / TotalEnrollments * 100m : 0;
    public decimal ReplyRate => TotalEnrollments > 0
        ? (decimal)TotalReplies / TotalEnrollments * 100m : 0;
}

public class SequenceStep
{
    public int Id { get; set; }
    public int SequenceId { get; set; }
    public int StepOrder { get; set; }
    public string StepType { get; set; } = "email";
    public int? TemplateId { get; set; }
    public int? WaitDays { get; set; }
    public int? WaitHours { get; set; }
    public string SubjectOverride { get; set; } = "";
    public string BodyOverride { get; set; } = "";
    public bool SkipWeekends { get; set; } = true;
}

public class LandingPage
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContentHtml { get; set; } = "";
    public int? CampaignId { get; set; }
    public bool IsPublished { get; set; }
    public int ViewCount { get; set; }
    public int SubmissionCount { get; set; }

    public decimal ConversionRate => ViewCount > 0
        ? (decimal)SubmissionCount / ViewCount * 100m : 0;
}

public class AttributionTouch
{
    public long Id { get; set; }
    public int DealId { get; set; }
    public int? CampaignId { get; set; }
    public string CampaignName { get; set; } = "";
    public string TouchType { get; set; } = "";
    public DateTime TouchedAt { get; set; }
    public decimal FirstTouchWeight { get; set; }
    public decimal LastTouchWeight { get; set; }
    public decimal LinearWeight { get; set; }
    public decimal PositionWeight { get; set; }
    public decimal TimeDecayWeight { get; set; }
}

public class CampaignInfluence
{
    public int CampaignId { get; set; }
    public string CampaignName { get; set; } = "";
    public decimal? Budget { get; set; }
    public decimal ActualCost { get; set; }
    public int InfluencedDeals { get; set; }
    public int WonDeals { get; set; }
    public decimal RevenueFirstTouch { get; set; }
    public decimal RevenueLastTouch { get; set; }
    public decimal RevenueLinear { get; set; }
    public decimal RevenuePosition { get; set; }
    public decimal RevenueTimeDecay { get; set; }
    public decimal? RoiLinear { get; set; }
}

// ─── Stage 2: Sales Operations ───────────────────────────────────────────────

public class Territory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public string TerritoryType { get; set; } = "geographic";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int MemberCount { get; set; }
    public int AccountCount { get; set; }
}

public class TerritoryRule
{
    public int Id { get; set; }
    public int TerritoryId { get; set; }
    public string RuleName { get; set; } = "";
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "equals";
    public string Value { get; set; } = "";
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

public class Quota
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public int? TerritoryId { get; set; }
    public string ProductCategory { get; set; } = "";
    public string PeriodType { get; set; } = "quarterly";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TargetAmount { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal RampPct { get; set; } = 100;

    public decimal AchievedAmount { get; set; }     // populated from live query
    public decimal AttainmentPct => TargetAmount > 0 ? AchievedAmount / TargetAmount * 100m : 0;
}

public class CommissionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PlanType { get; set; } = "flat";
    public decimal BaseRatePct { get; set; } = 10;
    public bool IsActive { get; set; } = true;
}

public class CommissionTier
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public int TierOrder { get; set; }
    public decimal MinAttainmentPct { get; set; }
    public decimal? MaxAttainmentPct { get; set; }
    public decimal RatePct { get; set; }
}

public class CommissionPayout
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal QuotaAmount { get; set; }
    public decimal AchievedAmount { get; set; }
    public decimal AttainmentPct { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal SpiffAmount { get; set; }
    public decimal ClawbackAmount { get; set; }
    public decimal NetPayout { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Status { get; set; } = "draft";
}

public class OpportunitySplit
{
    public int Id { get; set; }
    public int DealId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string SplitType { get; set; } = "revenue";
    public decimal CreditPct { get; set; }
    public string Role { get; set; } = "";
}

public class AccountTeamMember
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string TeamRole { get; set; } = "";
    public string AccessLevel { get; set; } = "read";
    public DateTime AddedAt { get; set; }
}

public class AccountPlan
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int? FiscalYear { get; set; }
    public decimal? AnnualTarget { get; set; }
    public string StrategicGoals { get; set; } = "";
    public string KnownInitiatives { get; set; } = "";
    public decimal? KnownBudget { get; set; }
    public string KnownBudgetPeriod { get; set; } = "";
    public string[] WhitespaceProducts { get; set; } = [];
    public DateTime? LastReviewedAt { get; set; }
    public DateTime? NextReviewAt { get; set; }
    public int? OwnerId { get; set; }
    public string Status { get; set; } = "active";
}

public class DealInsight
{
    public long Id { get; set; }
    public int DealId { get; set; }
    public string InsightType { get; set; } = "";
    public string Severity { get; set; } = "warn";
    public string Message { get; set; } = "";
    public DateTime DetectedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public bool IsResolved { get; set; }

    public bool IsCritical => Severity == "critical";
}

public class PipelineHealth
{
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = "";
    public int OpenDeals { get; set; }
    public decimal OpenPipeline { get; set; }
    public decimal WeightedPipeline { get; set; }
    public decimal CurrentQuota { get; set; }
    public decimal? CoverageRatio { get; set; }
    public int StalledDeals { get; set; }
    public int OverdueDeals { get; set; }

    public string HealthRating => CoverageRatio switch
    {
        >= 4 => "excellent",
        >= 3 => "healthy",
        >= 2 => "warning",
        _ => "critical"
    };
}

// ─── Stage 3: CPQ + Contracts + Revenue ──────────────────────────────────────

public class ProductBundle
{
    public int Id { get; set; }
    public int ParentProductId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<BundleComponent> Components { get; set; } = [];
}

public class BundleComponent
{
    public int Id { get; set; }
    public int BundleId { get; set; }
    public int ComponentProductId { get; set; }
    public string ComponentProductName { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public bool IsOptional { get; set; }
    public decimal? OverridePrice { get; set; }
    public int SortOrder { get; set; }
}

public class PricingRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RuleType { get; set; } = "";
    public int? ProductId { get; set; }
    public int? BundleId { get; set; }
    public decimal? MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public int? AccountId { get; set; }
    public string PromoCode { get; set; } = "";
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public int? MaxUses { get; set; }
    public int TimesUsed { get; set; }
    public decimal? DiscountPct { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? FixedPrice { get; set; }
    public int Priority { get; set; } = 100;
    public bool IsActive { get; set; } = true;
}

public class ApprovalRequest
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public int RequestedBy { get; set; }
    public string RequestedByName { get; set; } = "";
    public string ApprovalType { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string Priority { get; set; } = "normal";
    public string Reason { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public bool IsPending => Status == "pending";
    public bool IsOverdue => IsPending && ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

public class ApprovalStep
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int StepOrder { get; set; }
    public string StepType { get; set; } = "approval";
    public int? ApproverUserId { get; set; }
    public string ApproverName { get; set; } = "";
    public string ApproverRole { get; set; } = "";
    public bool IsParallel { get; set; }
    public bool RequiresAll { get; set; } = true;
    public string Status { get; set; } = "waiting";
    public string Comment { get; set; } = "";
    public DateTime? ActedAt { get; set; }
}

public class Contract
{
    public int Id { get; set; }
    public int? AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public int? DealId { get; set; }
    public string ContractNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContractType { get; set; } = "";
    public string Status { get; set; } = "draft";
    public decimal? ContractValue { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AutoRenew { get; set; }
    public int? RenewalTermMonths { get; set; }
    public int? RenewalNoticeDays { get; set; }
    public DateTime? SignedAt { get; set; }
    public string SignedByName { get; set; } = "";
    public string CounterParty { get; set; } = "";
    public int? OwnerId { get; set; }
    public string OwnerName { get; set; } = "";

    public bool IsSigned => !string.IsNullOrEmpty(SignedByName) || SignedAt.HasValue;
    public int? DaysToExpiry => EndDate.HasValue
        ? (int)(EndDate.Value - DateTime.UtcNow).TotalDays : null;
    public bool IsInRenewalWindow =>
        EndDate.HasValue && RenewalNoticeDays.HasValue &&
        (EndDate.Value - DateTime.UtcNow).TotalDays <= RenewalNoticeDays.Value &&
        EndDate.Value > DateTime.UtcNow;
}

public class ContractClause
{
    public int Id { get; set; }
    public string ClauseCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string BodyHtml { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsRequired { get; set; }
    public bool IsNegotiable { get; set; } = true;
    public int Version { get; set; } = 1;
    public bool LegalApproved { get; set; }
}

public class ContractMilestone
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string MilestoneType { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime DueDate { get; set; }
    public decimal? Amount { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? CompletedAt { get; set; }

    public bool IsOverdue => Status == "pending" && DueDate < DateTime.UtcNow.Date;
}

public class Subscription
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public int? ContractId { get; set; }
    public int? ProductId { get; set; }
    public string SubscriptionNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "active";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Mrr { get; set; }
    public decimal Arr { get; set; }
    public string Currency { get; set; } = "GBP";
    public string BillingPeriod { get; set; } = "monthly";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? CancelAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool AutoRenew { get; set; } = true;

    public bool IsActive => Status == "active";
    public bool IsTrial => Status == "trial";
    public bool IsChurned => Status == "cancelled";
}

public class SubscriptionEvent
{
    public long Id { get; set; }
    public int SubscriptionId { get; set; }
    public string EventType { get; set; } = "";
    public decimal? PreviousMrr { get; set; }
    public decimal? NewMrr { get; set; }
    public decimal? MrrDelta { get; set; }
    public decimal? PreviousQuantity { get; set; }
    public decimal? NewQuantity { get; set; }
    public string Reason { get; set; } = "";
    public DateTime OccurredAt { get; set; }

    public bool IsExpansion => MrrDelta > 0;
    public bool IsContraction => MrrDelta < 0;
}

public class RevenueSchedule
{
    public int Id { get; set; }
    public int? SubscriptionId { get; set; }
    public int? ContractId { get; set; }
    public int? OrderId { get; set; }
    public int? ProductId { get; set; }
    public string PerformanceObligation { get; set; } = "";
    public string RecognitionMethod { get; set; } = "ratable";
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int Periods { get; set; } = 1;
    public string Status { get; set; } = "scheduled";
}

public class RevenueEntry
{
    public int Id { get; set; }
    public int ScheduleId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime RecognizedAt { get; set; }
    public string GlJournalId { get; set; } = "";
    public bool IsReversed { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public int? QuoteId { get; set; }
    public int? DealId { get; set; }
    public int AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public int? ContractId { get; set; }
    public string Status { get; set; } = "draft";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime OrderDate { get; set; }
    public DateTime? FulfilledAt { get; set; }
    public DateTime? InvoicedAt { get; set; }
    public string PoNumber { get; set; } = "";
    public int? OwnerId { get; set; }

    public bool IsFulfilled => FulfilledAt.HasValue;
    public bool IsInvoiced => InvoicedAt.HasValue;
}

public class MrrDashboard
{
    public string Currency { get; set; } = "GBP";
    public int ActiveSubscriptions { get; set; }
    public decimal TotalMrr { get; set; }
    public decimal TotalArr { get; set; }
    public int TrialCount { get; set; }
    public int ChurnedLast30d { get; set; }
    public decimal MrrChurnedLast30d { get; set; }

    public decimal ChurnRatePct => ActiveSubscriptions > 0 && ChurnedLast30d > 0
        ? (decimal)ChurnedLast30d / (ActiveSubscriptions + ChurnedLast30d) * 100m : 0;
}
