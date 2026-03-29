using System.Security.Claims;
using Central.Tenancy;

namespace Central.Api.Middleware;

/// <summary>
/// Resolves the current tenant from the JWT token or X-Tenant header.
/// Runs AFTER authentication. Populates ITenantContext (scoped per request).
/// Unauthenticated requests or requests without tenant context default to tenant_default.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantContext = context.RequestServices.GetRequiredService<TenantContext>();

        // Skip tenant resolution for public endpoints
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/health") || path.StartsWith("/api/version") ||
            path.StartsWith("/api/register") || path.StartsWith("/api/webhooks"))
        {
            tenantContext.TenantSlug = "default";
            tenantContext.SchemaName = "public";
            tenantContext.IsResolved = true;
            await _next(context);
            return;
        }

        // Try JWT claim first
        var tenantClaim = context.User.FindFirst("tenant_slug");
        if (tenantClaim != null && !string.IsNullOrEmpty(tenantClaim.Value))
        {
            tenantContext.TenantSlug = tenantClaim.Value;
            tenantContext.SchemaName = tenantClaim.Value == "default" ? "public" : $"tenant_{tenantClaim.Value}";

            var tenantIdClaim = context.User.FindFirst("tenant_id");
            if (tenantIdClaim != null && Guid.TryParse(tenantIdClaim.Value, out var tid))
                tenantContext.TenantId = tid;

            var tierClaim = context.User.FindFirst("tenant_tier");
            if (tierClaim != null) tenantContext.Tier = tierClaim.Value;

            tenantContext.IsResolved = true;
            await _next(context);
            return;
        }

        // Try X-Tenant header (for API key auth or explicit tenant selection)
        if (context.Request.Headers.TryGetValue("X-Tenant", out var tenantHeader))
        {
            var slug = tenantHeader.FirstOrDefault();
            if (!string.IsNullOrEmpty(slug))
            {
                tenantContext.TenantSlug = slug;
                tenantContext.SchemaName = slug == "default" ? "public" : $"tenant_{slug}";
                tenantContext.IsResolved = true;
                await _next(context);
                return;
            }
        }

        // Default: single-tenant backward compatibility
        tenantContext.TenantSlug = "default";
        tenantContext.SchemaName = "public";
        tenantContext.Tier = "enterprise";
        tenantContext.IsResolved = true;

        await _next(context);
    }
}

public static class TenantResolutionExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
