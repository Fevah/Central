namespace Central.Core.Models;

// ─── Portal Users ────────────────────────────────────────────────────────────

public class PortalUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PortalType { get; set; } = "customer";  // customer, partner
    public int? ContactId { get; set; }
    public int? AccountId { get; set; }
    public string AccountName { get; set; } = "";
    public int? CompanyId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int LoginCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? EmailVerifiedAt { get; set; }

    public bool IsCustomer => PortalType == "customer";
    public bool IsPartner => PortalType == "partner";
    public bool IsVerified => EmailVerifiedAt.HasValue;
}

public class PartnerDealRegistration
{
    public int Id { get; set; }
    public int PartnerUserId { get; set; }
    public string PartnerEmail { get; set; } = "";
    public string CustomerCompanyName { get; set; } = "";
    public string CustomerContactName { get; set; } = "";
    public string CustomerContactEmail { get; set; } = "";
    public decimal? EstimatedValue { get; set; }
    public string Currency { get; set; } = "GBP";
    public string[] ProductsOfInterest { get; set; } = [];
    public string Notes { get; set; } = "";
    public string Status { get; set; } = "submitted";
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ConvertedDealId { get; set; }
    public string RejectionReason { get; set; } = "";

    public bool IsApproved => Status == "approved" || Status == "converted";
}

// ─── Knowledge Base ──────────────────────────────────────────────────────────

public class KbArticle
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public int? CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string Title { get; set; } = "";
    public string ContentHtml { get; set; } = "";
    public string Status { get; set; } = "draft";
    public string Visibility { get; set; } = "public";
    public int ViewCount { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    public string[] Tags { get; set; } = [];
    public DateTime? PublishedAt { get; set; }

    public bool IsPublished => Status == "published";
    public decimal HelpfulnessPct => (HelpfulCount + NotHelpfulCount) > 0
        ? (decimal)HelpfulCount / (HelpfulCount + NotHelpfulCount) * 100m : 0;
}

public class KbCategory
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int? ParentId { get; set; }
    public int SortOrder { get; set; } = 100;
    public bool IsPublic { get; set; } = true;
}

// ─── Community ───────────────────────────────────────────────────────────────

public class CommunityThread
{
    public int Id { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string BodyMarkdown { get; set; } = "";
    public int? AuthorUserId { get; set; }
    public int? AuthorPortalUserId { get; set; }
    public string AuthorName { get; set; } = "";
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public bool IsResolved { get; set; }
    public int ViewCount { get; set; }
    public int ReplyCount { get; set; }
    public int VoteScore { get; set; }
    public DateTime? LastReplyAt { get; set; }
    public string[] Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

public class CommunityPost
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int? ParentPostId { get; set; }
    public string BodyMarkdown { get; set; } = "";
    public int? AuthorUserId { get; set; }
    public int? AuthorPortalUserId { get; set; }
    public string AuthorName { get; set; } = "";
    public bool IsMarkedAnswer { get; set; }
    public int VoteScore { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─── Rule Engines ────────────────────────────────────────────────────────────

public class ValidationRule
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string RuleExpr { get; set; } = "";       // JSONLogic
    public string ErrorMessage { get; set; } = "";
    public string ErrorField { get; set; } = "";
    public string Severity { get; set; } = "error";
    public bool IsActive { get; set; } = true;
    public string[] AppliesOn { get; set; } = ["insert", "update"];
    public int Priority { get; set; } = 100;
}

public class WorkflowRule
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string TriggerType { get; set; } = "on_update";
    public string[] TriggerFields { get; set; } = [];
    public string ConditionExpr { get; set; } = "";  // JSONLogic
    public string ActionType { get; set; } = "elsa_workflow";
    public string ElsaWorkflowDefinitionId { get; set; } = "";
    public string InlineAction { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int ExecutionOrder { get; set; } = 100;
}

public class RuleExecutionEntry
{
    public long Id { get; set; }
    public string RuleType { get; set; } = "";
    public int RuleId { get; set; }
    public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    public DateTime TriggeredAt { get; set; }
    public string Result { get; set; } = "";
    public string Message { get; set; } = "";
    public string ElsaWorkflowInstanceId { get; set; } = "";
    public int? DurationMs { get; set; }

    public bool IsSuccess => Result is "pass" or "workflow_started";
}

// ─── Custom Objects ──────────────────────────────────────────────────────────

public class CustomEntity
{
    public int Id { get; set; }
    public string ApiName { get; set; } = "";
    public string Label { get; set; } = "";
    public string PluralLabel { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "#5B8AF5";
    public bool IsActive { get; set; } = true;
    public bool ShowInMenu { get; set; } = true;
    public string RecordNameFormat { get; set; } = "{{name}}";
}

public class CustomField
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public string ApiName { get; set; } = "";
    public string Label { get; set; } = "";
    public string FieldType { get; set; } = "text";
    public bool IsRequired { get; set; }
    public bool IsUnique { get; set; }
    public bool IsExternalId { get; set; }
    public string DefaultValue { get; set; } = "";
    public string HelpText { get; set; } = "";
    public string Placeholder { get; set; } = "";
    public int? MaxLength { get; set; }
    public int? Precision { get; set; }
    public int? Scale { get; set; }
    public string[] PicklistValues { get; set; } = [];
    public bool PicklistRestricted { get; set; }
    public string LookupEntity { get; set; } = "";
    public string ValidationRegex { get; set; } = "";
    public string ValidationMessage { get; set; } = "";
    public string Section { get; set; } = "";
    public int SortOrder { get; set; } = 100;
    public bool IsActive { get; set; } = true;

    public bool IsPicklist => FieldType is "picklist" or "multipick";
    public bool IsLookup => FieldType == "lookup";
    public bool IsNumeric => FieldType is "number" or "currency" or "percent";
}

public class FieldPermission
{
    public int Id { get; set; }
    public string RoleName { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string FieldName { get; set; } = "";
    public string Permission { get; set; } = "read";  // hidden, read, write

    public bool IsHidden => Permission == "hidden";
    public bool IsReadOnly => Permission == "read";
    public bool IsWritable => Permission == "write";
}

// ─── Import ──────────────────────────────────────────────────────────────────

public class ImportJob
{
    public int Id { get; set; }
    public string EntityType { get; set; } = "";
    public string SourceType { get; set; } = "csv";
    public string Filename { get; set; } = "";
    public long? FileSizeBytes { get; set; }
    public int RowCount { get; set; }
    public string Status { get; set; } = "pending";
    public string DedupStrategy { get; set; } = "create_new";
    public string DedupMatchField { get; set; } = "";
    public bool DryRun { get; set; } = true;
    public int RowsProcessed { get; set; }
    public int RowsCreated { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }
    public int RowsFailed { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ErrorMessage { get; set; } = "";

    public decimal CompletionPct => RowCount > 0 ? (decimal)RowsProcessed / RowCount * 100m : 0;
    public decimal SuccessPct => RowsProcessed > 0
        ? (decimal)(RowsCreated + RowsUpdated) / RowsProcessed * 100m : 0;
}

// ─── Commerce ────────────────────────────────────────────────────────────────

public class ShoppingCart
{
    public int Id { get; set; }
    public int? PortalUserId { get; set; }
    public string SessionToken { get; set; } = "";
    public int? AccountId { get; set; }
    public string Currency { get; set; } = "GBP";
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal Total { get; set; }
    public string PromoCode { get; set; } = "";
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsActive => Status == "active";
    public bool IsEmpty => Subtotal == 0;
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int? ProductId { get; set; }
    public int? BundleId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public string PaymentNumber { get; set; } = "";
    public int? OrderId { get; set; }
    public int? CartId { get; set; }
    public int? AccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public string Status { get; set; } = "pending";
    public string PaymentMethod { get; set; } = "";
    public string StripePaymentIntentId { get; set; } = "";
    public string Last4 { get; set; } = "";
    public string Brand { get; set; } = "";
    public DateTime? AuthorizedAt { get; set; }
    public DateTime? CapturedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string FailureCode { get; set; } = "";
    public string FailureMessage { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public bool IsSuccess => Status == "succeeded";
    public bool IsRefunded => Status == "refunded";
    public bool IsFailed => Status == "failed";
}
