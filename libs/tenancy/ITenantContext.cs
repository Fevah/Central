namespace Central.Tenancy;

/// <summary>
/// Scoped tenant context — populated per request by TenantResolutionMiddleware.
/// All data access reads TenantId/SchemaName from this context.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    string SchemaName { get; }
    string Tier { get; }
    bool IsResolved { get; }
}

public class TenantContext : ITenantContext
{
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = "default";
    public string SchemaName { get; set; } = "public";
    public string Tier { get; set; } = "enterprise";
    public bool IsResolved { get; set; }

    /// <summary>Default tenant for single-tenant / backward-compatible mode.</summary>
    public static TenantContext Default => new()
    {
        TenantId = Guid.Empty,
        TenantSlug = "default",
        SchemaName = "public",
        Tier = "enterprise",
        IsResolved = true
    };
}
