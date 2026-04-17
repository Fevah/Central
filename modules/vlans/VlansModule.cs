using Central.Engine.Auth;
using Central.Engine.Modules;

namespace Central.Module.VLANs;

/// <summary>
/// VLAN inventory and site VLAN management module.
/// </summary>
public class VlansModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "VLANs";
    public string PermissionCategory => "vlans";
    public int SortOrder => 50;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Devices", SortOrder, "vlans:read", page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("Refresh", P.VlansRead, null, () => { });
            });
            page.AddGroup("View", group =>
            {
                group.AddToggleButton("Show Default", P.VlansRead, isOn => { });
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("VLANs", panelId: "VlanPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
    }
}
