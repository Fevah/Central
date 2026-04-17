using Central.Engine.Auth;
using Central.Engine.Modules;
using Central.Engine.Services;

namespace Central.Desktop;

/// <summary>
/// Registers the 8 generic global actions — Add / Edit / Delete / Duplicate /
/// Refresh / Export / Undo / Redo — as real ribbon items on every major module
/// page (Home, Devices, Switches, Links, VLANs, Routing, Tasks, Service Desk,
/// Admin, Global Admin).
///
/// Because these buttons go through the normal module-ribbon → WireModuleRibbon
/// → SyncModuleRibbonToDbAsync pipeline, they end up as rows in
/// <c>ribbon_items</c> and therefore appear in the admin tree + user
/// "My Ribbon" tree, fully editable (reorder, rename, hide, change icon)
/// like any other ribbon item.
///
/// Click handlers go through <see cref="GlobalActionService.Dispatch"/>, which
/// routes to the active panel's VM command (if it inherits
/// <c>ListViewModelBase</c>) or falls back to the legacy grid-centric
/// dispatch. Permission gating is applied per action so a user who lacks e.g.
/// <c>devices:write</c> doesn't see Add/Edit/Duplicate on the Devices tab.
/// </summary>
public class GlobalActionsModule : IModule, IModuleRibbon
{
    public string Name => "Global Actions";
    public string PermissionCategory => "";
    // SortOrder 5 so this module registers before the concrete module buttons.
    // WireModuleRibbon merges same-captioned groups, so final button order in
    // the Actions group is: generic-globals, then module-specific extras.
    public int SortOrder => 5;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        foreach (var (pageHeader, moduleKey) in RibbonGlobalActionCatalogue.Modules)
        {
            // Capture for closure — required since loop variables are reused.
            var mod = moduleKey;
            var page = pageHeader;

            ribbon.AddPage(pageHeader, 0, p =>
            {
                p.AddGroup("Actions", g =>
                {
                    foreach (var (key, caption, glyph) in RibbonGlobalActionCatalogue.Actions)
                    {
                        var actionKey = key;
                        var perm = PermissionFor(mod, actionKey);

                        // AddLargeButton gives the Ribbon a 32x32 glyph to pair
                        // with the generic Add/Edit/Delete captions.
                        g.AddLargeButton(
                            content:   caption,
                            permission: perm,
                            largeGlyph: glyph,
                            toolTip:    $"{caption}  —  applies to the active grid on {page}",
                            onClick:    () => GlobalActionService.Instance.Dispatch(actionKey));
                    }
                });
            });
        }
    }

    /// <summary>
    /// Permission code for a (module, action) pair. Write-like actions require
    /// <c>{module}:write</c>, Delete requires <c>{module}:delete</c>, read-only
    /// actions (Refresh / Export / Undo / Redo) are unrestricted. The "home"
    /// pseudo-module always returns null — Home surfaces the active panel's
    /// module, not its own.
    /// </summary>
    private static string? PermissionFor(string moduleKey, string actionKey)
    {
        if (moduleKey == "home") return null;
        return actionKey switch
        {
            "add" or "edit" or "duplicate" => $"{moduleKey}:write",
            "delete"                       => $"{moduleKey}:delete",
            _                              => null
        };
    }
}
