using Central.Engine.Modules;
using Central.Engine.Services;

namespace Central.Desktop.Services;

/// <summary>
/// WPF-side <see cref="IModuleUpdatePolicy"/> for Phase 5 of the
/// module-update system (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
/// Uses the existing <see cref="NotificationService"/> toast queue
/// for every kind of event; richer UX (in-banner "Reload now" button,
/// countdown dialog for FullRestart) lands in Phase 5b when this
/// policy gets a dependency on the real banner + dialog widgets.
///
/// <para>For now the policy is honest about what's done vs. deferred:</para>
/// <list type="bullet">
/// <item><b>HotSwap applied</b> — <see cref="NotificationService.Success"/>
/// toast. Silent in the sense that no user decision is required.</item>
/// <item><b>SoftReload available</b> — <see cref="NotificationService.Info"/>
/// toast with a "reload when convenient" hint. Until a UI banner with
/// a click-through button lands, we don't call the apply action
/// automatically — user has to restart manually. This is explicitly
/// the conservative path; the alternative (auto-apply on SoftReload)
/// would be surprising.</item>
/// <item><b>FullRestart required</b> — <see cref="NotificationService.Warning"/>
/// toast. Actual scheduled-restart flow wires into existing
/// <c>UpdateManager.RestartApplication()</c> in Phase 5b.</item>
/// <item><b>Failure</b> — <see cref="NotificationService.Error"/> toast
/// with the reason. Exception details go to the log only; users see a
/// short reason.</item>
/// </list>
/// </summary>
public sealed class WpfModuleUpdatePolicy : IModuleUpdatePolicy
{
    public Task HandleHotSwapAppliedAsync(ModuleUpdateNotification n, CancellationToken ct)
    {
        NotificationService.Instance.Success(
            title:   $"{n.ModuleCode} updated to {n.ToVersion}",
            message: "Applied automatically — no action needed.",
            source:  "module-update");
        return Task.CompletedTask;
    }

    public Task HandleSoftReloadAsync(ModuleUpdateNotification n, Func<Task> applyAction, CancellationToken ct)
    {
        // Phase 5 conservative path: announce the update + wait for
        // the user to restart manually. Phase 5b replaces this with a
        // banner that has a "Reload now" button calling applyAction.
        // NOT invoking applyAction keeps the manager in the "update
        // available, not yet applied" state — the DB catalog still
        // reflects the new current_version, but the running module is
        // the old one until the user takes action.
        NotificationService.Instance.Info(
            title:   $"{n.ModuleCode} has a soft-reload update ({n.FromVersion ?? "?"} → {n.ToVersion})",
            message: "Reload the module when convenient. Restart the app to apply.",
            source:  "module-update");
        _ = applyAction; // reserved — see Phase 5b for the banner wiring
        return Task.CompletedTask;
    }

    public Task HandleFullRestartAsync(ModuleUpdateNotification n, CancellationToken ct)
    {
        NotificationService.Instance.Warning(
            title:   $"{n.ModuleCode} requires a full-client update ({n.FromVersion ?? "?"} → {n.ToVersion})",
            message: "This update changes an engine contract and needs a process restart. " +
                     "Restart at your earliest convenience to apply.",
            source:  "module-update");
        return Task.CompletedTask;
    }

    public Task HandleFailureAsync(ModuleUpdateNotification n, string reason, Exception? ex, CancellationToken ct)
    {
        var versionDisplay = n.FromVersion is null
            ? n.ToVersion
            : $"{n.FromVersion} → {n.ToVersion}";
        NotificationService.Instance.Error(
            title:   $"Module update failed: {n.ModuleCode} {versionDisplay}",
            message: reason,
            source:  "module-update");
        _ = ex; // stack trace goes to the app log by existing convention; toast stays short
        return Task.CompletedTask;
    }
}
