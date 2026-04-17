using System.Windows;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Widgets;
using Npgsql;

namespace Central.Module.CRM.Dashboards;

/// <summary>
/// CRM's platform-dashboard contribution — accounts, open deals, pipeline
/// value. Shown on the landing dashboard alongside other modules; the
/// module-specific <see cref="CrmDashboardPanel"/> has the full CRM detail.
/// </summary>
public class CrmDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "CRM";
    public int SortOrder => 40;
    public string? RequiredPermission => P.CrmRead;

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        int accounts = 0, openDeals = 0, leadsNew = 0;
        decimal pipeline = 0, weighted = 0;

        try
        {
            await using var conn = new NpgsqlConnection(dsn);
            await conn.OpenAsync(ct);

            await using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM crm_accounts WHERE is_active IS NOT FALSE", conn))
            {
                accounts = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
            }

            await using (var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*),
                       COALESCE(SUM(value), 0),
                       COALESCE(SUM(value * probability / 100.0), 0)
                FROM crm_deals
                WHERE stage NOT IN ('Closed Won','Closed Lost')", conn))
            {
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct))
                {
                    openDeals = r.GetInt32(0);
                    pipeline = r.GetDecimal(1);
                    weighted = r.GetDecimal(2);
                }
            }

            await using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM crm_leads WHERE status = 'new'", conn))
            {
                leadsNew = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
            }
        }
        catch { /* tables may not exist on a fresh tenant */ }

        return new UIElement[]
        {
            KpiCardBuilder.Build("Accounts",    accounts,        0, false),
            KpiCardBuilder.Build("Open Deals",  openDeals,       0, false),
            KpiCardBuilder.Build("Pipeline",    (double)pipeline, 0, false, "int"),
            KpiCardBuilder.Build("Weighted",    (double)weighted, 0, false, "int"),
            KpiCardBuilder.Build("New Leads",   leadsNew,        0, false),
        };
    }
}
