using Central.Engine.Auth;
using Central.Engine.Modules;

namespace Central.Module.Switches;

/// <summary>
/// Configured switches, connectivity (ping/SSH), running configs module.
/// </summary>
public class SwitchesModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Switches";
    public string PermissionCategory => "switches";
    public int SortOrder => 20;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Devices", SortOrder, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New Switch",    P.SwitchesWrite,  null, () => { });
                group.AddButton("Edit Switch",   P.SwitchesWrite,  null, () => { });
                group.AddButton("Delete Switch", P.SwitchesDelete, null, () => { });
            });
            page.AddGroup("Connectivity", group =>
            {
                group.AddButton("Ping All",      P.SwitchesPing, null, () => { });
                group.AddButton("Ping Selected", P.SwitchesPing, null, () => { });
                group.AddButton("Sync Config",   P.SwitchesSync, null, () => { });
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Switches", panelId: "SwitchesPanel");
                group.AddCheckButton("Details",  panelId: "SwitchDetailPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
    }
}
