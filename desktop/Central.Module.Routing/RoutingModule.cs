using Central.Core.Auth;
using Central.Core.Modules;

namespace Central.Module.Routing;

/// <summary>
/// BGP, VRRP, static routes, and future OSPF module.
/// </summary>
public class RoutingModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Routing";
    public string PermissionCategory => "bgp";
    public int SortOrder => 40;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Devices", SortOrder, "bgp:read", page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("Sync BGP",     P.BgpSync, null, () => { });
                group.AddButton("Sync All BGP", P.BgpSync, null, () => { });
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("BGP", panelId: "BgpPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
    }
}
