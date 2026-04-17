using Central.Engine.Auth;
using Central.Engine.Modules;

namespace Central.Engine.Shell;

/// <summary>
/// Registration record for a dockable panel contributed by a module.
/// </summary>
public class PanelRegistration
{
    public string Id { get; set; } = "";
    public string Caption { get; set; } = "";
    public Type ViewType { get; set; } = typeof(object);
    public Type ViewModelType { get; set; } = typeof(object);
    public string Permission { get; set; } = "";
    public DockPosition Dock { get; set; } = DockPosition.Document;
    public bool ClosedByDefault { get; set; }
}

/// <summary>
/// Fluent IPanelBuilder implementation that collects module panel contributions.
/// </summary>
public class PanelBuilder : IPanelBuilder
{
    public List<PanelRegistration> Panels { get; } = new();

    public void AddPanel(string id, string caption, Type viewType, Type viewModelType,
        string permission, DockPosition dock = DockPosition.Document,
        bool closedByDefault = false)
    {
        Panels.Add(new PanelRegistration
        {
            Id = id,
            Caption = caption,
            ViewType = viewType,
            ViewModelType = viewModelType,
            Permission = permission,
            Dock = dock,
            ClosedByDefault = closedByDefault
        });
    }

    /// <summary>Filter panels by current user's permissions.</summary>
    public List<PanelRegistration> GetVisiblePanels()
    {
        var auth = AuthContext.Instance;
        return Panels
            .Where(p => string.IsNullOrEmpty(p.Permission) || auth.HasPermission(p.Permission))
            .ToList();
    }
}
