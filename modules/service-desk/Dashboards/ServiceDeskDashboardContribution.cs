using System.Windows;
using Central.Engine.Models;
using Central.Engine.Widgets;
using Npgsql;

namespace Central.Module.ServiceDesk.Dashboards;

/// <summary>
/// Service Desk's platform-dashboard contribution — open / in-progress /
/// overdue ticket counts + technician load.
/// </summary>
public class ServiceDeskDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "Service Desk";
    public int SortOrder => 50;
    public string? RequiredPermission => "servicedesk:read";

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        int open = 0, inProgress = 0, overdue = 0, resolvedToday = 0;

        try
        {
            await using var conn = new NpgsqlConnection(dsn);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FILTER (WHERE status IN ('Open','Awaiting Response','On Hold')),
                       COUNT(*) FILTER (WHERE status = 'In Progress'),
                       COUNT(*) FILTER (WHERE due_by < NOW() AND status NOT IN ('Resolved','Closed','Cancelled','Canceled','Archive')),
                       COUNT(*) FILTER (WHERE resolved_at::date = CURRENT_DATE)
                FROM sd_requests", conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                open = r.GetInt32(0);
                inProgress = r.GetInt32(1);
                overdue = r.GetInt32(2);
                resolvedToday = r.GetInt32(3);
            }
        }
        catch { /* sd_requests absent — SD not provisioned for this tenant */ }

        return new UIElement[]
        {
            KpiCardBuilder.Build("Open",             open,          0, lowerIsBetter: true),
            KpiCardBuilder.Build("In Progress",      inProgress,    0, false),
            KpiCardBuilder.Build("Overdue",          overdue,       0, lowerIsBetter: true),
            KpiCardBuilder.Build("Resolved Today",   resolvedToday, 0, false),
        };
    }
}
