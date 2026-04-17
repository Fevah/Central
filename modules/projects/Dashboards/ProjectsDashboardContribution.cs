using System.Windows;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Widgets;
using Npgsql;

namespace Central.Module.Projects.Dashboards;

/// <summary>
/// Projects module's dashboard contribution — open / in-progress / completed /
/// overdue task counts across the tenant.
/// </summary>
public class ProjectsDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "Projects & Tasks";
    public int SortOrder => 30;
    public string? RequiredPermission => P.TasksRead;

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        int open = 0, inProgress = 0, completed = 0, overdue = 0;

        try
        {
            await using var conn = new NpgsqlConnection(dsn);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*) FILTER (WHERE status IN ('New','Open','Reopened')),
                       COUNT(*) FILTER (WHERE status = 'In Progress'),
                       COUNT(*) FILTER (WHERE status IN ('Closed','Done','Resolved')),
                       COUNT(*) FILTER (WHERE due_date < NOW() AND status NOT IN ('Closed','Done','Resolved'))
                FROM tasks", conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                open = r.GetInt32(0);
                inProgress = r.GetInt32(1);
                completed = r.GetInt32(2);
                overdue = r.GetInt32(3);
            }
        }
        catch { /* table absent */ }

        return new UIElement[]
        {
            KpiCardBuilder.Build("Open",        open,       0, lowerIsBetter: true),
            KpiCardBuilder.Build("In Progress", inProgress, 0, false),
            KpiCardBuilder.Build("Completed",   completed,  0, false),
            KpiCardBuilder.Build("Overdue",     overdue,    0, lowerIsBetter: true),
        };
    }
}
