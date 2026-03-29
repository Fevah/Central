using Npgsql;
using Central.Tenancy;

namespace Central.Licensing;

/// <summary>
/// Manages tenant subscriptions — tier enforcement, upgrades, limits.
/// </summary>
public class SubscriptionService
{
    private readonly string _platformDsn;

    public SubscriptionService(string platformDsn) => _platformDsn = platformDsn;

    /// <summary>Get the current subscription for a tenant.</summary>
    public async Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT s.id, s.tenant_id, s.plan_id, s.status, s.started_at, s.expires_at, s.stripe_sub_id,
                     p.tier, p.display_name, p.max_users, p.max_devices
              FROM central_platform.tenant_subscriptions s
              JOIN central_platform.subscription_plans p ON p.id = s.plan_id
              WHERE s.tenant_id = @tid AND s.status IN ('active','trial')
              ORDER BY s.started_at DESC LIMIT 1", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new TenantSubscription
        {
            Id = r.GetGuid(0), TenantId = r.GetGuid(1), PlanId = r.GetInt32(2),
            Status = r.GetString(3), StartedAt = r.GetDateTime(4),
            ExpiresAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
            StripeSubId = r.IsDBNull(6) ? null : r.GetString(6)
        };
    }

    /// <summary>Check if a tenant is within their subscription limits.</summary>
    public async Task<LimitCheckResult> CheckLimitsAsync(Guid tenantId, string tenantSchema)
    {
        var sub = await GetSubscriptionAsync(tenantId);
        if (sub == null)
            return new LimitCheckResult { IsWithinLimits = false, Reason = "No active subscription" };

        // Get plan limits
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var planCmd = new NpgsqlCommand(
            "SELECT max_users, max_devices FROM central_platform.subscription_plans WHERE id = @pid", conn);
        planCmd.Parameters.AddWithValue("pid", sub.PlanId);
        await using var pr = await planCmd.ExecuteReaderAsync();
        if (!await pr.ReadAsync()) return new LimitCheckResult { IsWithinLimits = true };
        var maxUsers = pr.IsDBNull(0) ? (int?)null : pr.GetInt32(0);
        var maxDevices = pr.IsDBNull(1) ? (int?)null : pr.GetInt32(1);
        await pr.CloseAsync();

        // Count current usage in tenant schema
        if (maxUsers.HasValue)
        {
            await using var userCmd = new NpgsqlCommand(
                $"SELECT COUNT(*) FROM {tenantSchema}.app_users WHERE is_active = true", conn);
            var userCount = (long)(await userCmd.ExecuteScalarAsync())!;
            if (userCount >= maxUsers.Value)
                return new LimitCheckResult { IsWithinLimits = false, Reason = $"User limit reached ({maxUsers})", CurrentUsers = (int)userCount, MaxUsers = maxUsers.Value };
        }

        if (maxDevices.HasValue)
        {
            await using var devCmd = new NpgsqlCommand(
                $"SELECT COUNT(*) FROM {tenantSchema}.switch_guide WHERE is_deleted IS NOT TRUE", conn);
            var deviceCount = (long)(await devCmd.ExecuteScalarAsync())!;
            if (deviceCount >= maxDevices.Value)
                return new LimitCheckResult { IsWithinLimits = false, Reason = $"Device limit reached ({maxDevices})", CurrentDevices = (int)deviceCount, MaxDevices = maxDevices.Value };
        }

        // Check expiry
        if (sub.ExpiresAt.HasValue && sub.ExpiresAt.Value < DateTime.UtcNow)
            return new LimitCheckResult { IsWithinLimits = false, Reason = "Subscription expired" };

        return new LimitCheckResult { IsWithinLimits = true };
    }

    /// <summary>Upgrade a tenant's subscription.</summary>
    public async Task<bool> UpgradeAsync(Guid tenantId, string newTier)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();

        // Get plan ID for new tier
        await using var planCmd = new NpgsqlCommand(
            "SELECT id FROM central_platform.subscription_plans WHERE tier = @t", conn);
        planCmd.Parameters.AddWithValue("t", newTier);
        var planId = await planCmd.ExecuteScalarAsync();
        if (planId == null) return false;

        // Cancel current subscription
        await using var cancelCmd = new NpgsqlCommand(
            "UPDATE central_platform.tenant_subscriptions SET status = 'cancelled' WHERE tenant_id = @tid AND status IN ('active','trial')", conn);
        cancelCmd.Parameters.AddWithValue("tid", tenantId);
        await cancelCmd.ExecuteNonQueryAsync();

        // Create new subscription
        await using var newCmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_subscriptions (tenant_id, plan_id, status)
              VALUES (@tid, @pid, 'active')", conn);
        newCmd.Parameters.AddWithValue("tid", tenantId);
        newCmd.Parameters.AddWithValue("pid", (int)planId);
        await newCmd.ExecuteNonQueryAsync();

        // Update tenant tier
        await using var tierCmd = new NpgsqlCommand(
            "UPDATE central_platform.tenants SET tier = @t WHERE id = @tid", conn);
        tierCmd.Parameters.AddWithValue("t", newTier);
        tierCmd.Parameters.AddWithValue("tid", tenantId);
        await tierCmd.ExecuteNonQueryAsync();

        return true;
    }

    /// <summary>Get all subscription plans.</summary>
    public async Task<List<SubscriptionPlan>> GetPlansAsync()
    {
        var plans = new List<SubscriptionPlan>();
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, tier, display_name, max_users, max_devices, price_monthly FROM central_platform.subscription_plans ORDER BY price_monthly", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            plans.Add(new SubscriptionPlan
            {
                Id = r.GetInt32(0), Tier = r.GetString(1), DisplayName = r.GetString(2),
                MaxUsers = r.IsDBNull(3) ? null : r.GetInt32(3),
                MaxDevices = r.IsDBNull(4) ? null : r.GetInt32(4),
                PriceMonthly = r.IsDBNull(5) ? null : r.GetDecimal(5)
            });
        return plans;
    }
}

public class LimitCheckResult
{
    public bool IsWithinLimits { get; set; }
    public string? Reason { get; set; }
    public int? CurrentUsers { get; set; }
    public int? MaxUsers { get; set; }
    public int? CurrentDevices { get; set; }
    public int? MaxDevices { get; set; }
}
