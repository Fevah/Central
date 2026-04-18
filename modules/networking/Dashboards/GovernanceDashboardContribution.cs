using System.Windows;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Widgets;
using Npgsql;

namespace Central.Module.Networking.Dashboards;

/// <summary>
/// Governance tile on the landing dashboard: per-status counts of the
/// tenant's Change Sets + the last-10-min audit activity count.
/// Surfaces the governance queue state without asking admins to open
/// the Change Sets / Audit panels.
///
/// <para>Queries <c>net.change_set</c> and <c>net.audit_entry</c>
/// directly rather than routing through the Rust networking-engine —
/// a dashboard tile needs to render fast, and a local DSN query is
/// faster than an HTTP round-trip. The engine's authoritative read
/// paths remain the canonical source for everything that changes
/// tenant state.</para>
///
/// <para>Hidden when the tenant id isn't resolved yet (e.g. during
/// offline mode or for users whose tenant lookup failed) — returning
/// an empty card list tells the dashboard host to skip the section
/// entirely per the <see cref="IDashboardContribution"/> contract.</para>
/// </summary>
public class GovernanceDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "Networking Governance";
    public int SortOrder => 25;   // Sits between the existing Networking (20) and Devices (30) sections.
    public string? RequiredPermission => P.ChangeSetsRead;

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        var tenantId = AuthContext.Instance.CurrentTenantId;
        if (tenantId == System.Guid.Empty) return System.Array.Empty<UIElement>();

        int draft = 0, submitted = 0, approved = 0, applied = 0;
        int failedApply = 0, auditLast10 = 0;

        await using var conn = new NpgsqlConnection(dsn);
        await conn.OpenAsync(ct);

        // Change Set counts per status — single query, one trip. Schema-
        // absent (net.change_set not migrated yet) is swallowed so the
        // tile stays off for greenfield tenants rather than erroring.
        try
        {
            await using var cmd = new NpgsqlCommand(@"
                SELECT
                    COUNT(*) FILTER (WHERE status::text = 'Draft'),
                    COUNT(*) FILTER (WHERE status::text = 'Submitted'),
                    COUNT(*) FILTER (WHERE status::text = 'Approved'),
                    COUNT(*) FILTER (WHERE status::text = 'Applied'),
                    COUNT(*) FILTER (WHERE EXISTS (
                        SELECT 1 FROM net.change_set_item i
                         WHERE i.change_set_id = cs.id
                           AND i.apply_error IS NOT NULL))
                FROM net.change_set cs
                WHERE cs.organization_id = @org AND cs.deleted_at IS NULL", conn);
            cmd.Parameters.AddWithValue("org", tenantId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                draft       = (int)r.GetInt64(0);
                submitted   = (int)r.GetInt64(1);
                approved    = (int)r.GetInt64(2);
                applied     = (int)r.GetInt64(3);
                failedApply = (int)r.GetInt64(4);
            }
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { /* net.change_set not present — skip */ }
        catch (NpgsqlException) { /* any wire / table absence — skip */ }

        // Recent audit activity — last 10 minutes. Gives an immediate
        // "is the engine being used right now?" signal.
        try
        {
            await using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*)
                  FROM net.audit_entry
                 WHERE organization_id = @org
                   AND created_at >= now() - interval '10 minutes'", conn);
            cmd.Parameters.AddWithValue("org", tenantId);
            auditLast10 = (int)(long)(await cmd.ExecuteScalarAsync(ct) ?? 0L);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01") { /* net.audit_entry not present — skip */ }
        catch (NpgsqlException) { /* wire issue — skip */ }

        // Five cards — mirrors the per-status counts admins see in the
        // Change Sets panel header, plus the last-10-min pulse. failed
        // apply is the one that should trigger an "open the panel"
        // reaction, so it gets lowerIsBetter framing.
        var cards = new List<UIElement>
        {
            KpiCardBuilder.Build("Drafts",             draft,       0, false),
            KpiCardBuilder.Build("Awaiting approval",  submitted,   0, false),
            KpiCardBuilder.Build("Approved (pending apply)", approved, 0, false),
            KpiCardBuilder.Build("Applied",            applied,     0, false),
            KpiCardBuilder.Build("With apply errors",  failedApply, 0, lowerIsBetter: true),
            KpiCardBuilder.Build("Audit (last 10m)",   auditLast10, 0, false),
        };
        return cards;
    }
}
