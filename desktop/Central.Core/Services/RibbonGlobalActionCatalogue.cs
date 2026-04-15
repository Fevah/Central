namespace Central.Core.Services;

/// <summary>
/// Canonical list of the 8 global ribbon actions (Add, Edit, Delete, …) and
/// the module pages they get injected into. Shared between
/// <see cref="GlobalActionService"/> (dispatch + icon resolution), the ribbon
/// admin tree (editable rows), and GlobalActionsModule (module-time ribbon
/// registration). Keep this as the one source of truth — adding a new global
/// action or a new module page is a one-liner here that flows everywhere.
/// </summary>
public static class RibbonGlobalActionCatalogue
{
    /// <summary>(actionKey, displayCaption, defaultDxGlyph)</summary>
    public static readonly (string Key, string Caption, string DefaultGlyph)[] Actions =
    {
        ("add",       "Add",       "AddItem_32x32"),
        ("edit",      "Edit",      "Edit_32x32"),
        ("delete",    "Delete",    "Delete_32x32"),
        ("duplicate", "Duplicate", "Copy_32x32"),
        ("refresh",   "Refresh",   "Refresh_32x32"),
        ("export",    "Export",    "ExportToXLS_32x32"),
        ("undo",      "Undo",      "Undo_32x32"),
        ("redo",      "Redo",      "Redo_32x32"),
    };

    /// <summary>
    /// (ribbonPageHeader, lowercase moduleKey used in item_key).
    /// Only includes pages that actually EXIST as live ribbon tabs. Modules
    /// that register under another page (e.g. Links/VLANs/Routing all
    /// register under "Devices") are NOT listed — their global actions are
    /// covered by the parent page's entry.
    /// </summary>
    public static readonly (string PageHeader, string ModuleKey)[] Modules =
    {
        ("Home",         "home"),
        ("Devices",      "devices"),   // Links + VLANs + Routing + Switches all merge here
        ("Switches",     "switches"),  // has its own XAML page (hidden until panel active)
        ("Tasks",        "tasks"),
        ("Service Desk", "servicedesk"),
        ("Admin",        "admin"),
        ("Global Admin", "globaladmin"),
    };
}
