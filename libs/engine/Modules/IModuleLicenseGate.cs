namespace Central.Engine.Modules;

/// <summary>
/// Tenant-scoped decision: "is this module enabled for the current tenant?"
/// The shell calls this for each module before instantiation. A `false`
/// answer means the module is never loaded — its .NET type isn't
/// Activator.CreateInstance'd, its ribbon tab isn't registered, its panels
/// aren't built, its dashboard contributions aren't registered. Exactly the
/// "disable for a tenant and the whole module is gone" semantics we promised.
///
/// Required modules (currently just <c>Central.Module.Global</c>) bypass the
/// gate — there is no configuration where those are absent.
/// </summary>
public interface IModuleLicenseGate
{
    /// <summary>
    /// Return true if the module with the given <paramref name="moduleCode"/>
    /// should load for the current tenant. Module codes are the PermissionCategory
    /// string each module already exposes (e.g. "devices", "networking", "crm").
    /// </summary>
    bool IsEnabled(string moduleCode);
}

/// <summary>
/// Default gate for local dev / offline mode / pre-tenant-aware bootstrap.
/// Every module is enabled. Replace with a DB-backed gate (reading
/// <c>tenant_module_licenses</c> via <c>Central.Licensing.ModuleLicenseService</c>)
/// once the current tenant is known — that happens post-login.
/// </summary>
public sealed class AllowAllModuleGate : IModuleLicenseGate
{
    public bool IsEnabled(string moduleCode) => true;
}

/// <summary>
/// Gate backed by a concrete allow-list. Useful for tests and for the
/// transition period where we hard-code per-tenant toggles rather than read
/// them from the database.
/// </summary>
public sealed class AllowListModuleGate : IModuleLicenseGate
{
    private readonly HashSet<string> _allowed;

    public AllowListModuleGate(IEnumerable<string> allowedModuleCodes)
    {
        _allowed = new HashSet<string>(allowedModuleCodes, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEnabled(string moduleCode) => _allowed.Contains(moduleCode);
}
