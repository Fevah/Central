using Central.Engine.Modules;
using Central.Licensing;

namespace Central.Desktop.Auth;

/// <summary>
/// Tenant-aware module gate backed by <c>central_platform.tenant_module_licenses</c>
/// via <see cref="ModuleLicenseService"/>. Reads the tenant's full module licence
/// list once at construction, caches the allowed codes, then <see cref="IsEnabled"/>
/// is a hot-path HashSet lookup.
///
/// Use via the <see cref="CreateForTenantAsync"/> factory — it does the async
/// load, so callers get a fully populated gate back.
///
/// If the tenant has no rows in <c>tenant_module_licenses</c> (e.g. brand-new
/// tenant before licensing is configured) the gate falls back to allow-all.
/// The platform-level "is base" flag in the catalogue is honoured — base
/// modules are always allowed regardless of explicit licence rows.
/// </summary>
public sealed class DbModuleLicenseGate : IModuleLicenseGate
{
    private readonly HashSet<string> _allowed;
    private readonly bool _fallbackAllowAll;

    private DbModuleLicenseGate(HashSet<string> allowed, bool fallbackAllowAll)
    {
        _allowed = allowed;
        _fallbackAllowAll = fallbackAllowAll;
    }

    public bool IsEnabled(string moduleCode)
        => _fallbackAllowAll || _allowed.Contains(moduleCode);

    /// <summary>
    /// Build a gate for the given tenant. Loads the current licence set from
    /// the platform DB. Returns an allow-all fallback gate if the tenant
    /// hasn't had any modules provisioned yet, so a brand-new tenant can
    /// still see the UI before admin wires up licences.
    /// </summary>
    public static async Task<DbModuleLicenseGate> CreateForTenantAsync(string platformDsn, Guid tenantId)
    {
        var service = new ModuleLicenseService(platformDsn);
        var modules = await service.GetModulesAsync(tenantId);

        // Empty list = tenant hasn't been set up yet. Don't lock them out
        // of the app; allow everything until admin configures licences.
        if (modules.Count == 0)
            return new DbModuleLicenseGate(new HashSet<string>(StringComparer.OrdinalIgnoreCase), fallbackAllowAll: true);

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in modules)
        {
            // "Base" modules are always enabled regardless of licence rows
            // (equivalent to the _alwaysRequired set on the Bootstrapper side).
            if (m.IsBase || m.IsLicensed)
                allowed.Add(m.Code);
        }
        return new DbModuleLicenseGate(allowed, fallbackAllowAll: false);
    }
}
