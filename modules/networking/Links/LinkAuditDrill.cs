using System;
using System.Linq;
using System.Threading.Tasks;
using Central.ApiClient;
using Central.Engine.Models;
using Central.Engine.Shell;

namespace Central.Module.Networking.Links;

/// <summary>
/// Shared helper for the P2P / B2B / FW grid context menus.
/// <para>
/// Each of the three grids has a row type that implements
/// <see cref="INetworkLink"/>. The audit drill is identical across
/// them: read <c>LinkId</c> (the link_code), resolve it to a
/// <c>net.link</c> uuid via the engine's thin list endpoint, and
/// publish the two-message drill-down pair that the Audit panel's
/// <c>selectEntity</c> handler consumes.
/// </para>
/// <para>
/// Lives outside each grid's code-behind because the three grids
/// pre-date INotifyPropertyChanged unification and keep the
/// code-behind minimal; this helper stays static + stateless so
/// there's nothing to construct per-grid.
/// </para>
/// </summary>
internal static class LinkAuditDrill
{
    /// <summary>Resolve the incoming link_code via the engine and
    /// fire the audit drill-down. Swallows engine errors — the
    /// context menu is a nice-to-have and the grid itself must
    /// keep working on transient engine failures.</summary>
    public static async Task ShowAuditForLinkAsync(
        string? baseUrl, Guid tenantId, int? actorUserId,
        INetworkLink link)
    {
        if (link is null) return;
        if (string.IsNullOrWhiteSpace(link.LinkId)) return;
        if (string.IsNullOrEmpty(baseUrl) || tenantId == Guid.Empty) return;

        try
        {
            using var client = new NetworkingEngineClient(baseUrl);
            if (actorUserId is int uid) client.SetActorUserId(uid);

            var all = await client.ListLinksAsync(tenantId);
            var match = all.FirstOrDefault(l =>
                string.Equals(l.LinkCode, link.LinkId, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                // Dual-write gap — link exists in legacy p2p_links /
                // b2b_links / fw_links but not in net.link yet.
                // Open the audit panel anyway with a broad filter so
                // the operator sees the entity-type view rather than
                // a silent no-op.
                PanelMessageBus.Publish(new OpenPanelMessage("audit"));
                PanelMessageBus.Publish(new NavigateToPanelMessage(
                    "audit", "selectEntity:Link:00000000-0000-0000-0000-000000000000"));
                return;
            }

            PanelMessageBus.Publish(new OpenPanelMessage("audit"));
            PanelMessageBus.Publish(new NavigateToPanelMessage(
                "audit", $"selectEntity:Link:{match.Id}"));
        }
        catch { /* silent — grid keeps working */ }
    }

    /// <summary>Copy the link_code to the clipboard. Used by the
    /// shared "Copy link code" context-menu item.</summary>
    public static void CopyLinkCode(INetworkLink? link)
    {
        if (link is null) return;
        if (string.IsNullOrWhiteSpace(link.LinkId)) return;
        try { System.Windows.Clipboard.SetText(link.LinkId); } catch { /* ignore */ }
    }
}
