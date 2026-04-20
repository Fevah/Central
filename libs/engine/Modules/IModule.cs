using System.Reflection;

namespace Central.Engine.Modules;

/// <summary>
/// Base contract for all Central modules.
/// Each module is an Autofac.Module that also implements this interface
/// to provide metadata for the shell.
///
/// Versioning (added 2026-04-20 for Phase 1 of the module-update system,
/// see <c>docs/MODULE_UPDATE_SYSTEM.md</c>): <see cref="Version"/> and
/// <see cref="EngineContractVersion"/> are append-only default-
/// implementations so every existing module picks them up with no code
/// change. Modules that want to advertise something other than their
/// assembly's informational version override <see cref="Version"/>;
/// modules that have been recompiled against a newer
/// <c>Central.Engine</c> surface bump <see cref="EngineContractVersion"/>
/// so the shell can refuse to load them against an older engine.
/// </summary>
public interface IModule
{
    /// <summary>Display name — "Devices", "Links", "Routing"</summary>
    string Name { get; }

    /// <summary>Permission category — "devices", "links", "bgp"</summary>
    string PermissionCategory { get; }

    /// <summary>Ribbon tab sort order (10, 20, 30...)</summary>
    int SortOrder { get; }

    /// <summary>
    /// Module semantic version. Default reads
    /// <c>AssemblyInformationalVersionAttribute</c> from the concrete
    /// module's assembly — CI stamps this from the git tag
    /// <c>module/{code}/{version}</c>. Falls back to
    /// <c>AssemblyVersion</c> then <c>"0.0.0"</c> so a module without
    /// any version attribute still returns a parseable string rather
    /// than null.
    /// </summary>
    string Version =>
        GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? GetType().Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    /// <summary>
    /// The <c>Central.Engine</c> interface contract version this module
    /// was compiled against. Host refuses to load a module whose
    /// <see cref="EngineContractVersion"/> exceeds the host's own
    /// current contract version (loading a module built against a
    /// newer engine risks missing-method crashes). Default: 1, the
    /// initial contract version shipped 2026-04-20.
    /// </summary>
    int EngineContractVersion => 1;
}

/// <summary>
/// Host-side constant for the current engine contract version. Bump
/// this only in a PR that also ships the engine-contract-check CI
/// update (see Phase 6 in <c>docs/MODULE_UPDATE_SYSTEM.md</c>) —
/// accidental bumps break every deployed module.
/// </summary>
public static class EngineContract
{
    /// <summary>
    /// Current host engine-contract version. Modules advertising
    /// <see cref="IModule.EngineContractVersion"/> greater than this
    /// are rejected at module-load time. Adding a new method to
    /// <see cref="IModule"/> with a default implementation does NOT
    /// require a bump — default impls are backward-compatible.
    /// Removing or changing a method signature DOES require a bump +
    /// every module to recompile against the new surface.
    /// </summary>
    public const int CurrentVersion = 1;
}
