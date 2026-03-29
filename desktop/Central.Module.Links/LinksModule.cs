using Central.Core.Auth;
using Central.Core.Modules;

namespace Central.Module.Links;

/// <summary>
/// P2P, B2B, and FW link management module.
/// </summary>
public class LinksModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Links";
    public string PermissionCategory => "links";
    public int SortOrder => 30;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Devices", SortOrder, "links:read", page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New Link",    P.LinksWrite,  null, () => { });
                group.AddButton("Delete Link", P.LinksDelete, null, () => { });
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("P2P",  panelId: "P2PPanel");
                group.AddCheckButton("B2B",  panelId: "B2BPanel");
                group.AddCheckButton("FW",   panelId: "FWPanel");
            });
            page.AddGroup("Config", group =>
            {
                group.AddButton("Build Config", P.LinksRead, null, () => { });
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels registered when views are extracted
    }
}
