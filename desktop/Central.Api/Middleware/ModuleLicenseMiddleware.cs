using Central.Licensing;
using Central.Tenancy;

namespace Central.Api.Middleware;

/// <summary>
/// Checks module licensing per request. If tenant doesn't have a license for the
/// requested module, returns 403 Forbidden with the module requirement.
/// Maps API paths to module codes.
/// </summary>
public class ModuleLicenseMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Dictionary<string, string> PathToModule = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/api/devices"]      = "devices",
        ["/api/switches"]     = "switches",
        ["/api/links"]        = "links",
        ["/api/bgp"]          = "routing",
        ["/api/vlans"]        = "vlans",
        ["/api/tasks"]        = "tasks",
        ["/api/projects"]     = "tasks",
        ["/api/appointments"] = "tasks",
        ["/api/admin"]        = "admin",
        ["/api/identity"]     = "admin",
        ["/api/keys"]         = "admin",
        ["/api/audit"]        = "audit",
        ["/api/servicedesk"]  = "servicedesk",
        ["/api/global-admin"] = "globaladmin",
    };

    public ModuleLicenseMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip license check for non-module paths
        var moduleCode = ResolveModule(path);
        if (moduleCode == null)
        {
            await _next(context);
            return;
        }

        // Skip if not authenticated or no tenant resolved
        var tenantContext = context.RequestServices.GetRequiredService<TenantContext>();
        if (!tenantContext.IsResolved || tenantContext.TenantId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        // Enterprise tier bypasses module checks
        if (tenantContext.Tier == "enterprise")
        {
            await _next(context);
            return;
        }

        // Check module license
        var licenseSvc = context.RequestServices.GetRequiredService<ModuleLicenseService>();
        var isLicensed = await licenseSvc.IsModuleLicensedAsync(tenantContext.TenantId, moduleCode);

        if (!isLicensed)
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Module not licensed",
                module = moduleCode,
                message = $"Your subscription does not include the '{moduleCode}' module. Upgrade your plan or activate this module."
            });
            return;
        }

        await _next(context);
    }

    private static string? ResolveModule(string path)
    {
        foreach (var (prefix, module) in PathToModule)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return module;
        }
        return null;
    }
}

public static class ModuleLicenseExtensions
{
    public static IApplicationBuilder UseModuleLicenseCheck(this IApplicationBuilder app)
        => app.UseMiddleware<ModuleLicenseMiddleware>();
}
