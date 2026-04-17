using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

// ── Groups ────────────────────────────────────────────────────────────────

public class GroupRecord : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _description = "";
    private string _groupType = "static";
    private string _ruleExpression = "";
    private bool _isActive = true;
    private int _memberCount;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string GroupType { get => _groupType; set { _groupType = value; N(); } }
    public string RuleExpression { get => _ruleExpression; set { _ruleExpression = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int MemberCount { get => _memberCount; set { _memberCount = value; N(); } }
    public bool IsDynamic => GroupType == "dynamic";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool AutoAssigned { get; set; }
    public DateTime AddedAt { get; set; }
}

public class GroupAssignmentRule
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string RuleName { get; set; } = "";
    public string RuleType { get; set; } = "";   // department, title, role, email_domain, custom
    public string RuleValue { get; set; } = "";
    public int Priority { get; set; } = 100;
    public bool IsEnabled { get; set; } = true;
}

// ── Feature Flags ─────────────────────────────────────────────────────────

public class FeatureFlag
{
    public int Id { get; set; }
    public string FlagKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool DefaultEnabled { get; set; }
    public string Category { get; set; } = "";
}

public class TenantFeatureFlag
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string FlagKey { get; set; } = "";
    public bool IsEnabled { get; set; }
    public int RolloutPct { get; set; } = 100;
    public DateTime? EnabledAt { get; set; }
    public DateTime? DisabledAt { get; set; }
}

// ── Security ──────────────────────────────────────────────────────────────

public class IpAccessRule
{
    public int Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Cidr { get; set; } = "";
    public string RuleType { get; set; } = "allow";
    public string Label { get; set; } = "";
    public string AppliesTo { get; set; } = "api";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class UserSshKey
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Label { get; set; } = "";
    public string KeyType { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class DeprovisioningRule
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string RuleType { get; set; } = "";   // inactivity_days, idp_removed, manager_requested
    public int? ThresholdDays { get; set; }
    public string Action { get; set; } = "disable";
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }
}

public class TermsOfService
{
    public int Id { get; set; }
    public string Version { get; set; } = "";
    public string ContentUrl { get; set; } = "";
    public DateTime EffectiveDate { get; set; }
    public bool RequiresAcceptance { get; set; } = true;
    public DateTime PublishedAt { get; set; }
}

public class DomainVerification
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string Domain { get; set; } = "";
    public string VerificationToken { get; set; } = "";
    public string Method { get; set; } = "dns_txt";
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

// ── Team Extensions ───────────────────────────────────────────────────────

public class TeamResource
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public string ResourceType { get; set; } = "";
    public int ResourceId { get; set; }
    public string AccessLevel { get; set; } = "read";
    public DateTime AddedAt { get; set; }
}

public class CompanyUserRole
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int UserId { get; set; }
    public string RoleName { get; set; } = "";
    public DateTime AssignedAt { get; set; }
}

public class TeamActivityEntry
{
    public long Id { get; set; }
    public int TeamId { get; set; }
    public int? UserId { get; set; }
    public string Username { get; set; } = "";
    public string ActivityType { get; set; } = "";
    public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    public string Description { get; set; } = "";
    public DateTime OccurredAt { get; set; }
}

// ── Permission Inheritance ────────────────────────────────────────────────

public class UserPermissionOverride
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string PermissionCode { get; set; } = "";
    public bool IsGranted { get; set; }
    public string Reason { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Social Auth ───────────────────────────────────────────────────────────

public class SocialProvider
{
    public int Id { get; set; }
    public string Provider { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string AuthorizeUrl { get; set; } = "";
    public string TokenUrl { get; set; } = "";
    public string UserinfoUrl { get; set; } = "";
    public string Scope { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string ButtonColor { get; set; } = "";
}

public class UserSocialLogin
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string ExternalEmail { get; set; } = "";
    public DateTime LinkedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

// ── Billing Extended ──────────────────────────────────────────────────────

public class SubscriptionAddon
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal PriceMonthly { get; set; }
    public decimal? PriceAnnual { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DiscountCode
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string DiscountType { get; set; } = "percent";
    public decimal DiscountValue { get; set; }
    public int? MaxUses { get; set; }
    public int TimesUsed { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PaymentMethod
{
    public int Id { get; set; }
    public int BillingAccountId { get; set; }
    public string MethodType { get; set; } = "";
    public string StripePmId { get; set; } = "";
    public string Last4 { get; set; } = "";
    public string Brand { get; set; } = "";
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }
    public bool IsDefault { get; set; }
    public string PoNumber { get; set; } = "";
    public DateTime? PoExpiresAt { get; set; }
}

public class UsageQuota
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public string QuotaType { get; set; } = "";
    public decimal LimitValue { get; set; }
    public decimal CurrentUsage { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string OverageAction { get; set; } = "warn";

    public bool IsExceeded => CurrentUsage >= LimitValue;
    public decimal UsagePct => LimitValue > 0 ? CurrentUsage / LimitValue * 100m : 0m;
}

// ── Password Reset / Email Verification ───────────────────────────────────

public class PasswordResetRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public class EmailVerificationRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
}

// ── Address History ──────────────────────────────────────────────────────

public class AddressHistoryEntry
{
    public long Id { get; set; }
    public int? AddressId { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string Action { get; set; } = "";
    public string OldValues { get; set; } = "";
    public string NewValues { get; set; } = "";
    public int? ChangedBy { get; set; }
    public string ChangedByName { get; set; } = "";
    public DateTime ChangedAt { get; set; }
}
