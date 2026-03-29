namespace Central.Core.Modules;

/// <summary>
/// Module that contributes dockable panels.
/// </summary>
public interface IModulePanels
{
    void RegisterPanels(IPanelBuilder panels);
}

public enum DockPosition { Document, Left, Right, Bottom }

public interface IPanelBuilder
{
    void AddPanel(string id, string caption, Type viewType, Type viewModelType,
        string permission, DockPosition dock = DockPosition.Document,
        bool closedByDefault = false);
}
