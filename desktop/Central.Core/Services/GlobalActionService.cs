using System.ComponentModel;
using System.Windows.Input;
using Central.Core.Auth;
using Central.Core.Widgets;

namespace Central.Core.Services;

/// <summary>
/// Central dispatcher for the 8 global ribbon actions — New, Edit, Delete,
/// Duplicate, Refresh, Export, Undo, Redo.
///
/// Every module tab's copy of these buttons resolves its icon, visibility,
/// tooltip, and target command through this service. Per-module overrides
/// (icon + visibility) live in admin_ribbon_defaults / user_ribbon_overrides
/// keyed on (page_name = moduleKey, item_name = actionKey). The concrete
/// resolution is pluggable via SetOverrideProvider — the shell wires a
/// provider at startup after PreloadIconOverridesAsync.
///
/// The active panel is resolved by a callback the shell supplies
/// (<see cref="SetActiveViewModelResolver"/>). When the user clicks a button
/// the service fetches that VM and dispatches to its command — so on Devices
/// tab the same "New" button creates a device, on Links it creates a link.
/// </summary>
public sealed class GlobalActionService : INotifyPropertyChanged
{
    public const string ActionAdd       = "add";
    public const string ActionEdit      = "edit";
    public const string ActionDelete    = "delete";
    public const string ActionDuplicate = "duplicate";
    public const string ActionRefresh   = "refresh";
    public const string ActionExport    = "export";
    public const string ActionUndo      = "undo";
    public const string ActionRedo      = "redo";

    /// <summary>
    /// Back-compat alias for <see cref="ActionAdd"/>. Kept so any callers that
    /// referenced "new" continue to resolve — the canonical term is "add"
    /// (matches <c>IActionTarget.AddCommand</c> and legacy ribbon_items rows).
    /// </summary>
    [Obsolete("Use ActionAdd — the canonical term is 'add', matching IActionTarget.AddCommand and existing ribbon_items rows.")]
    public const string ActionNew = ActionAdd;

    public static readonly string[] AllActions =
    {
        ActionAdd, ActionEdit, ActionDelete, ActionDuplicate,
        ActionRefresh, ActionExport, ActionUndo, ActionRedo
    };

    public static GlobalActionService Instance { get; } = new();

    private Func<IActionTarget?>? _activeVmResolver;
    private Func<string, string, GlobalActionOverride?>? _overrideProvider;
    private Action<string>? _fallbackDispatcher;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Called by the shell to tell the service how to find the active panel's VM.</summary>
    public void SetActiveViewModelResolver(Func<IActionTarget?> resolver)
        => _activeVmResolver = resolver;

    /// <summary>
    /// Wire a per-module icon/visibility override lookup. Given (moduleKey, actionKey)
    /// the provider returns null (inherit defaults) or an override record.
    /// </summary>
    public void SetOverrideProvider(Func<string, string, GlobalActionOverride?> provider)
        => _overrideProvider = provider;

    /// <summary>
    /// Shell-supplied fallback dispatcher invoked when the active panel doesn't
    /// expose a <see cref="IActionTarget"/> — typical for UserControl panels
    /// that drive a DevExpress grid directly. The fallback is responsible for
    /// the legacy dispatch (view.AddNewRow(), DeleteFocusedRow(), etc.).
    /// </summary>
    public void SetFallbackDispatcher(Action<string> dispatcher)
        => _fallbackDispatcher = dispatcher;

    /// <summary>Fire when overrides are reloaded from DB so UI can refresh icons/visibility.</summary>
    public void RaiseOverridesChanged()
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllActions)));

    // ── Dispatch ────────────────────────────────────────────────────────

    /// <summary>
    /// Run a global action against the currently active panel. Returns false if
    /// no VM is active or the action isn't wired. Never throws — the shell wraps
    /// callers with CommandGuard, and exceptions are logged by the VM.
    /// </summary>
    public bool Dispatch(string actionKey)
    {
        var vm = _activeVmResolver?.Invoke();
        if (vm != null)
        {
            var cmd = vm.GetActionCommand(actionKey);
            if (cmd != null && cmd.CanExecute(null))
            {
                cmd.Execute(null);
                return true;
            }
        }

        // No VM on active panel — fall through to shell-supplied legacy dispatcher
        if (_fallbackDispatcher != null)
        {
            _fallbackDispatcher(actionKey);
            return true;
        }

        return false;
    }

    /// <summary>Returns the ICommand on the active VM for a given action, or null.</summary>
    public ICommand? ResolveCommand(string actionKey)
    {
        var vm = _activeVmResolver?.Invoke();
        return vm?.GetActionCommand(actionKey);
    }

    // ── Icon / visibility / tooltip resolution ──────────────────────────

    /// <summary>
    /// Resolve the icon for a button. Lookup order:
    ///   user_ribbon_overrides(moduleKey, actionKey) — user override
    ///   admin_ribbon_defaults(moduleKey, actionKey) — admin per-module override
    ///   admin_ribbon_defaults("global", actionKey)  — admin root override
    ///   hardcoded DX default glyph for the action
    /// </summary>
    public string ResolveIconGlyph(string moduleKey, string actionKey)
    {
        var ov = _overrideProvider?.Invoke(moduleKey, actionKey);
        if (!string.IsNullOrWhiteSpace(ov?.IconGlyph)) return ov!.IconGlyph!;

        var rootOv = _overrideProvider?.Invoke("global", actionKey);
        if (!string.IsNullOrWhiteSpace(rootOv?.IconGlyph)) return rootOv!.IconGlyph!;

        return DefaultIconGlyph(actionKey);
    }

    /// <summary>Is this action visible on this module tab? Combines admin/user override with permission check.</summary>
    public bool IsVisible(string moduleKey, string actionKey)
    {
        // Module-level override takes precedence — admin can hide Delete on Links only
        var ov = _overrideProvider?.Invoke(moduleKey, actionKey);
        if (ov?.IsVisible == false) return false;

        // Global override (e.g. hide Redo site-wide for Viewer role)
        var rootOv = _overrideProvider?.Invoke("global", actionKey);
        if (rootOv?.IsVisible == false) return false;

        // Permission check — CRUD actions gate on moduleKey:write / :delete
        return HasPermission(moduleKey, actionKey);
    }

    private static bool HasPermission(string moduleKey, string actionKey)
    {
        // "home" is the aggregator — no specific permission; follow active VM
        if (moduleKey == "home" || moduleKey == "global") return true;

        return actionKey switch
        {
            ActionAdd       => AuthContext.Instance.HasPermission($"{moduleKey}:write"),
            ActionEdit      => AuthContext.Instance.HasPermission($"{moduleKey}:write"),
            ActionDuplicate => AuthContext.Instance.HasPermission($"{moduleKey}:write"),
            ActionDelete    => AuthContext.Instance.HasPermission($"{moduleKey}:delete"),
            // Read-only actions — always visible if user can see the module at all
            ActionRefresh or ActionExport or ActionUndo or ActionRedo => true,
            _ => true
        };
    }

    /// <summary>Tooltip/caption for the active context — e.g. "New Device" vs "New Link".</summary>
    public string ResolveCaption(string actionKey)
    {
        var vm = _activeVmResolver?.Invoke();
        var typeName = vm?.TypeNameForCaption ?? "";
        var verb = actionKey switch
        {
            ActionAdd       => "Add",
            ActionEdit      => "Edit",
            ActionDelete    => "Delete",
            ActionDuplicate => "Duplicate",
            ActionRefresh   => "Refresh",
            ActionExport    => "Export",
            ActionUndo      => "Undo",
            ActionRedo      => "Redo",
            _ => actionKey
        };
        return string.IsNullOrEmpty(typeName) ? verb : $"{verb} {typeName}";
    }

    /// <summary>Hardcoded default DX glyph — the "root" icon inherited by all modules.</summary>
    public static string DefaultIconGlyph(string actionKey) => actionKey switch
    {
        ActionAdd       => "AddItem_32x32",
        ActionEdit      => "Edit_32x32",
        ActionDelete    => "Delete_32x32",
        ActionDuplicate => "Copy_32x32",
        ActionRefresh   => "Refresh_32x32",
        ActionExport    => "ExportToXLS_32x32",
        ActionUndo      => "Undo_32x32",
        ActionRedo      => "Redo_32x32",
        _               => "Info_32x32"
    };
}

/// <summary>Override record returned by the configured provider.</summary>
public sealed class GlobalActionOverride
{
    public string? IconGlyph { get; init; }  // DX image name OR path to SVG/PNG
    public bool? IsVisible { get; init; }     // null = inherit
}
