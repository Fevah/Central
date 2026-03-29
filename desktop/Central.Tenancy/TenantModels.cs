namespace Central.Tenancy;

/// <summary>Tenant record from the central_platform schema.</summary>
public class Tenant
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Domain { get; set; }
    public bool IsActive { get; set; } = true;
    public string Tier { get; set; } = "free";
    public DateTime CreatedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
}

/// <summary>Subscription plan definition.</summary>
public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Tier { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int? MaxUsers { get; set; }
    public int? MaxDevices { get; set; }
    public decimal? PriceMonthly { get; set; }
}

/// <summary>A tenant's active subscription.</summary>
public class TenantSubscription
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public int PlanId { get; set; }
    public string Status { get; set; } = "active";
    public DateTime StartedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? StripeSubId { get; set; }
}

/// <summary>A licensable module in the platform.</summary>
public class ModuleLicense
{
    public int ModuleId { get; set; }
    public string Code { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsBase { get; set; }
    public bool IsLicensed { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

/// <summary>Global user record (cross-tenant).</summary>
public class GlobalUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TenantMembership> Memberships { get; set; } = new();
}

/// <summary>A user's membership in a tenant.</summary>
public class TenantMembership
{
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = "";
    public string TenantName { get; set; } = "";
    public string Role { get; set; } = "Viewer";
    public DateTime JoinedAt { get; set; }
}

/// <summary>Environment connection profile.</summary>
public class EnvironmentProfile
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string EnvironmentType { get; set; } = "live";
    public string ApiUrl { get; set; } = "";
    public string? SignalRUrl { get; set; }
    public string? CertFingerprint { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>Client version info for update management.</summary>
public class ClientVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; } = "";
    public string Platform { get; set; } = "windows-x64";
    public string? PackageUrl { get; set; }
    public string? DeltaFrom { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime PublishedAt { get; set; }
    public bool IsMandatory { get; set; }
}
