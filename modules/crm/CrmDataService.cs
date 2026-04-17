using System.Collections.ObjectModel;
using Central.Engine.Models;
using Npgsql;

namespace Central.Module.CRM;

/// <summary>Data access for CRM panels — wraps SQL queries for accounts/deals/leads/activities.</summary>
public class CrmDataService
{
    private readonly string _dsn;

    public CrmDataService(string dsn) => _dsn = dsn;

    public async Task<ObservableCollection<CrmAccount>> LoadAccountsAsync(CancellationToken ct = default)
    {
        var list = new ObservableCollection<CrmAccount>();
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT a.id, COALESCE(a.name,'') AS name, COALESCE(a.account_type,'customer') AS account_type,
                   a.industry, a.rating, a.source, a.stage, a.annual_revenue, a.employee_count,
                   a.account_owner_id, COALESCE(u.display_name,'') AS owner_name,
                   a.last_activity_at, a.next_follow_up, a.website, a.created_at, a.company_id
            FROM crm_accounts a
            LEFT JOIN app_users u ON u.id = a.account_owner_id
            WHERE a.is_deleted IS NOT TRUE
            ORDER BY a.name", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new CrmAccount
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                AccountType = r.GetString(2),
                Industry = r.IsDBNull(3) ? "" : r.GetString(3),
                Rating = r.IsDBNull(4) ? "" : r.GetString(4),
                Source = r.IsDBNull(5) ? "" : r.GetString(5),
                Stage = r.IsDBNull(6) ? "" : r.GetString(6),
                AnnualRevenue = r.IsDBNull(7) ? null : r.GetDecimal(7),
                EmployeeCount = r.IsDBNull(8) ? null : r.GetInt32(8),
                AccountOwnerId = r.IsDBNull(9) ? null : r.GetInt32(9),
                AccountOwnerName = r.GetString(10),
                LastActivityAt = r.IsDBNull(11) ? null : r.GetDateTime(11),
                NextFollowUp = r.IsDBNull(12) ? null : r.GetDateTime(12),
                Website = r.IsDBNull(13) ? "" : r.GetString(13),
                CreatedAt = r.IsDBNull(14) ? null : r.GetDateTime(14),
                CompanyId = r.IsDBNull(15) ? null : r.GetInt32(15)
            });
        }
        return list;
    }

    public async Task<ObservableCollection<CrmDeal>> LoadDealsAsync(CancellationToken ct = default)
    {
        var list = new ObservableCollection<CrmDeal>();
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT d.id, d.title, d.description, d.value, d.currency,
                   d.stage_id, d.stage, d.probability, d.expected_close, d.actual_close,
                   d.account_id, COALESCE(a.name,'') AS account_name,
                   d.contact_id, COALESCE(c.first_name || ' ' || c.last_name,'') AS contact_name,
                   d.owner_id, COALESCE(u.display_name,'') AS owner_name,
                   d.source, d.competitor, d.loss_reason, d.next_step,
                   d.created_at, d.updated_at
            FROM crm_deals d
            LEFT JOIN crm_accounts a ON a.id = d.account_id
            LEFT JOIN contacts c ON c.id = d.contact_id
            LEFT JOIN app_users u ON u.id = d.owner_id
            WHERE d.is_deleted IS NOT TRUE
            ORDER BY d.updated_at DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new CrmDeal
            {
                Id = r.GetInt32(0),
                Title = r.GetString(1),
                Description = r.IsDBNull(2) ? "" : r.GetString(2),
                Value = r.IsDBNull(3) ? null : r.GetDecimal(3),
                Currency = r.IsDBNull(4) ? "GBP" : r.GetString(4),
                StageId = r.IsDBNull(5) ? null : r.GetInt32(5),
                Stage = r.IsDBNull(6) ? "" : r.GetString(6),
                Probability = r.IsDBNull(7) ? 50 : r.GetInt32(7),
                ExpectedClose = r.IsDBNull(8) ? null : r.GetDateTime(8),
                ActualClose = r.IsDBNull(9) ? null : r.GetDateTime(9),
                AccountId = r.IsDBNull(10) ? null : r.GetInt32(10),
                AccountName = r.GetString(11),
                ContactId = r.IsDBNull(12) ? null : r.GetInt32(12),
                ContactName = r.GetString(13),
                OwnerId = r.IsDBNull(14) ? null : r.GetInt32(14),
                OwnerName = r.GetString(15),
                Source = r.IsDBNull(16) ? "" : r.GetString(16),
                Competitor = r.IsDBNull(17) ? "" : r.GetString(17),
                LossReason = r.IsDBNull(18) ? "" : r.GetString(18),
                NextStep = r.IsDBNull(19) ? "" : r.GetString(19),
                CreatedAt = r.IsDBNull(20) ? null : r.GetDateTime(20),
                UpdatedAt = r.IsDBNull(21) ? null : r.GetDateTime(21)
            });
        }
        return list;
    }

    public async Task<List<DealStage>> LoadDealStagesAsync(CancellationToken ct = default)
    {
        var list = new List<DealStage>();
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, sort_order, probability, is_won, is_lost, color FROM crm_deal_stages WHERE is_active = true ORDER BY sort_order", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new DealStage
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                SortOrder = r.GetInt32(2),
                Probability = r.GetInt32(3),
                IsWon = r.GetBoolean(4),
                IsLost = r.GetBoolean(5),
                Color = r.IsDBNull(6) ? "#808080" : r.GetString(6)
            });
        }
        return list;
    }

    public async Task UpdateDealStageAsync(int dealId, string newStage, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "UPDATE crm_deals SET stage = @s, updated_at = NOW() WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("s", newStage);
        cmd.Parameters.AddWithValue("id", dealId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ObservableCollection<CrmLead>> LoadLeadsAsync(CancellationToken ct = default)
    {
        var list = new ObservableCollection<CrmLead>();
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT l.id, COALESCE(l.first_name,''), COALESCE(l.last_name,''),
                   l.email, l.phone, l.company_name, l.title, l.source, l.status, l.score,
                   l.owner_id, COALESCE(u.display_name,'') AS owner_name,
                   l.converted_at, l.created_at
            FROM crm_leads l
            LEFT JOIN app_users u ON u.id = l.owner_id
            WHERE l.is_deleted IS NOT TRUE
            ORDER BY l.score DESC, l.created_at DESC LIMIT 500", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new CrmLead
            {
                Id = r.GetInt32(0),
                FirstName = r.GetString(1),
                LastName = r.GetString(2),
                Email = r.IsDBNull(3) ? "" : r.GetString(3),
                Phone = r.IsDBNull(4) ? "" : r.GetString(4),
                CompanyName = r.IsDBNull(5) ? "" : r.GetString(5),
                Title = r.IsDBNull(6) ? "" : r.GetString(6),
                Source = r.IsDBNull(7) ? "" : r.GetString(7),
                Status = r.IsDBNull(8) ? "new" : r.GetString(8),
                Score = r.IsDBNull(9) ? 0 : r.GetInt32(9),
                OwnerId = r.IsDBNull(10) ? null : r.GetInt32(10),
                OwnerName = r.GetString(11),
                ConvertedAt = r.IsDBNull(12) ? null : r.GetDateTime(12),
                CreatedAt = r.IsDBNull(13) ? null : r.GetDateTime(13)
            });
        }
        return list;
    }

    public async Task<CrmKpiSummary> LoadKpiSummaryAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT
              (SELECT COUNT(*) FROM crm_accounts WHERE is_deleted IS NOT TRUE AND account_type='customer'),
              (SELECT COUNT(*) FROM crm_accounts WHERE is_deleted IS NOT TRUE AND account_type='prospect'),
              (SELECT COUNT(*) FROM crm_deals   WHERE is_deleted IS NOT TRUE AND actual_close IS NULL),
              (SELECT COALESCE(SUM(value),0) FROM crm_deals WHERE is_deleted IS NOT TRUE AND actual_close IS NULL),
              (SELECT COALESCE(SUM(value*probability/100.0),0) FROM crm_deals WHERE is_deleted IS NOT TRUE AND actual_close IS NULL),
              (SELECT COALESCE(SUM(value),0) FROM crm_deals WHERE stage='Closed Won' AND actual_close >= date_trunc('month',NOW())),
              (SELECT COUNT(*) FROM crm_leads WHERE status='new' AND is_deleted IS NOT TRUE),
              (SELECT COUNT(*) FROM crm_activities WHERE due_at IS NOT NULL AND is_completed=false AND due_at < NOW())", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return new CrmKpiSummary();
        return new CrmKpiSummary
        {
            Customers = r.GetInt64(0),
            Prospects = r.GetInt64(1),
            OpenDeals = r.GetInt64(2),
            OpenPipelineValue = r.GetDecimal(3),
            WeightedPipeline = r.GetDecimal(4),
            RevenueThisMonth = r.GetDecimal(5),
            NewLeads = r.GetInt64(6),
            OverdueActivities = r.GetInt64(7)
        };
    }

    public async Task<List<PipelineStageSummary>> LoadPipelineSummaryAsync(CancellationToken ct = default)
    {
        var list = new List<PipelineStageSummary>();
        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(@"
            SELECT d.stage, COUNT(*), COALESCE(SUM(d.value),0), COALESCE(SUM(d.value*d.probability/100.0),0)
            FROM crm_deals d WHERE d.is_deleted IS NOT TRUE AND d.actual_close IS NULL
            GROUP BY d.stage
            ORDER BY MIN(COALESCE((SELECT sort_order FROM crm_deal_stages s WHERE s.name = d.stage), 100))", conn);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            list.Add(new PipelineStageSummary
            {
                Stage = r.IsDBNull(0) ? "(none)" : r.GetString(0),
                DealCount = r.GetInt64(1),
                TotalValue = r.GetDecimal(2),
                WeightedValue = r.GetDecimal(3)
            });
        }
        return list;
    }
}

public class CrmKpiSummary
{
    public long Customers { get; set; }
    public long Prospects { get; set; }
    public long OpenDeals { get; set; }
    public decimal OpenPipelineValue { get; set; }
    public decimal WeightedPipeline { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public long NewLeads { get; set; }
    public long OverdueActivities { get; set; }
}

public class PipelineStageSummary
{
    public string Stage { get; set; } = "";
    public long DealCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal WeightedValue { get; set; }
}
