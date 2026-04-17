using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>Billing extensions — addons, discounts, payment methods, quotas, proration.</summary>
public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder group)
    {
        // ── Addons catalog ──
        group.MapGet("/addons", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM central_platform.subscription_addons WHERE is_active = true ORDER BY name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/tenants/{tenantId:guid}/addons", async (Guid tenantId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT ta.*, a.name, a.price_monthly, a.price_annual
                FROM central_platform.tenant_addons ta
                JOIN central_platform.subscription_addons a ON a.code = ta.addon_code
                WHERE ta.tenant_id = @t AND (ta.ends_at IS NULL OR ta.ends_at > NOW())", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/tenants/{tenantId:guid}/addons", async (Guid tenantId, JsonElement body, DbConnectionFactory db) =>
        {
            var code = body.GetProperty("addon_code").GetString() ?? "";
            var qty = body.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1;
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO central_platform.tenant_addons (tenant_id, addon_code, quantity)
                VALUES (@t, @c, @q)
                ON CONFLICT (tenant_id, addon_code) DO UPDATE SET quantity = @q, ends_at = NULL
                RETURNING id", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("c", code);
            cmd.Parameters.AddWithValue("q", qty);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id, addon_code = code, quantity = qty });
        });

        // ── Discount codes ──
        group.MapGet("/discounts", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM central_platform.discount_codes WHERE is_active = true ORDER BY code", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/discounts", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO central_platform.discount_codes (code, description, discount_type, discount_value, max_uses, valid_from, valid_to)
                VALUES (@code, @desc, @type, @val, @mx, @from, @to) RETURNING id", conn);
            cmd.Parameters.AddWithValue("code", body.GetProperty("code").GetString() ?? "");
            cmd.Parameters.AddWithValue("desc", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("type", body.TryGetProperty("discount_type", out var t) ? t.GetString() ?? "percent" : "percent");
            cmd.Parameters.AddWithValue("val", body.GetProperty("discount_value").GetDecimal());
            cmd.Parameters.AddWithValue("mx", body.TryGetProperty("max_uses", out var mx) && mx.ValueKind == JsonValueKind.Number ? mx.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("from", body.TryGetProperty("valid_from", out var vf) && vf.ValueKind == JsonValueKind.String ? DateTime.Parse(vf.GetString()!) : DBNull.Value);
            cmd.Parameters.AddWithValue("to", body.TryGetProperty("valid_to", out var vt) && vt.ValueKind == JsonValueKind.String ? DateTime.Parse(vt.GetString()!) : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/billing/discounts/{id}", new { id });
        });

        // Apply discount
        group.MapPost("/tenants/{tenantId:guid}/redeem-discount", async (Guid tenantId, JsonElement body, DbConnectionFactory db) =>
        {
            var code = body.GetProperty("code").GetString() ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var lookup = new NpgsqlCommand(@"
                SELECT id, discount_type, discount_value, max_uses, times_used
                FROM central_platform.discount_codes
                WHERE code = @c AND is_active = true
                  AND (valid_from IS NULL OR valid_from <= CURRENT_DATE)
                  AND (valid_to IS NULL OR valid_to >= CURRENT_DATE)", conn);
            lookup.Parameters.AddWithValue("c", code);
            await using var r = await lookup.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound("Invalid or expired discount code");
            var discountId = r.GetInt32(0);
            var discountType = r.GetString(1);
            var discountValue = r.GetDecimal(2);
            var maxUses = r.IsDBNull(3) ? (int?)null : r.GetInt32(3);
            var timesUsed = r.GetInt32(4);
            await r.CloseAsync();

            if (maxUses.HasValue && timesUsed >= maxUses.Value)
                return ApiProblem.Conflict("Discount code has reached usage limit");

            await using var redeem = new NpgsqlCommand(@"
                INSERT INTO central_platform.discount_redemptions (discount_code_id, tenant_id)
                VALUES (@d, @t)
                ON CONFLICT (discount_code_id, tenant_id) DO NOTHING RETURNING id", conn);
            redeem.Parameters.AddWithValue("d", discountId);
            redeem.Parameters.AddWithValue("t", tenantId);
            var rid = await redeem.ExecuteScalarAsync();
            if (rid is null) return ApiProblem.Conflict("Discount already applied to this tenant");

            // Apply to subscription
            await using var apply = new NpgsqlCommand(@"
                UPDATE central_platform.tenant_subscriptions
                SET discount_pct = CASE WHEN @type = 'percent' THEN @val ELSE discount_pct END,
                    discount_reason = @code
                WHERE tenant_id = @t", conn);
            apply.Parameters.AddWithValue("t", tenantId);
            apply.Parameters.AddWithValue("type", discountType);
            apply.Parameters.AddWithValue("val", discountValue);
            apply.Parameters.AddWithValue("code", code);
            await apply.ExecuteNonQueryAsync();

            await using var incr = new NpgsqlCommand(
                "UPDATE central_platform.discount_codes SET times_used = times_used + 1 WHERE id = @d", conn);
            incr.Parameters.AddWithValue("d", discountId);
            await incr.ExecuteNonQueryAsync();

            return Results.Ok(new { redeemed = true, discount_type = discountType, discount_value = discountValue });
        });

        // ── Payment methods ──
        group.MapGet("/tenants/{tenantId:guid}/payment-methods", async (Guid tenantId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT pm.* FROM central_platform.payment_methods pm
                JOIN central_platform.billing_accounts ba ON ba.id = pm.billing_account_id
                WHERE ba.tenant_id = @t ORDER BY pm.is_default DESC", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── Quotas ──
        group.MapGet("/tenants/{tenantId:guid}/quotas", async (Guid tenantId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM central_platform.usage_quotas WHERE tenant_id = @t", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/tenants/{tenantId:guid}/quotas", async (Guid tenantId, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO central_platform.usage_quotas (tenant_id, quota_type, limit_value, overage_action)
                VALUES (@t, @qt, @lv, @oa)
                ON CONFLICT (tenant_id, quota_type, period_start) DO UPDATE SET limit_value = @lv, overage_action = @oa
                RETURNING id", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("qt", body.GetProperty("quota_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("lv", body.GetProperty("limit_value").GetDecimal());
            cmd.Parameters.AddWithValue("oa", body.TryGetProperty("overage_action", out var oa) ? oa.GetString() ?? "warn" : "warn");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id });
        });

        // ── Proration ──
        group.MapPost("/tenants/{tenantId:guid}/proration", async (Guid tenantId, JsonElement body, DbConnectionFactory db) =>
        {
            var eventType = body.GetProperty("event_type").GetString() ?? "";
            var prevPlan = body.TryGetProperty("prev_plan", out var pp) ? pp.GetString() ?? "" : "";
            var newPlan = body.TryGetProperty("new_plan", out var np) ? np.GetString() ?? "" : "";

            // Simple proration — credit remaining days of prev plan, charge full new plan
            decimal credit = 0, charge = 0;
            if (body.TryGetProperty("prev_monthly", out var pm) && body.TryGetProperty("new_monthly", out var nm)
                && body.TryGetProperty("days_remaining", out var dr))
            {
                var prevMonthly = pm.GetDecimal();
                var newMonthly = nm.GetDecimal();
                var daysRemaining = dr.GetInt32();
                credit = prevMonthly * daysRemaining / 30m;
                charge = newMonthly * daysRemaining / 30m;
            }

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO central_platform.proration_events
                    (tenant_id, event_type, prev_plan, new_plan, amount_credited, amount_charged)
                VALUES (@t, @et, @pp, @np, @c, @ch) RETURNING id", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            cmd.Parameters.AddWithValue("et", eventType);
            cmd.Parameters.AddWithValue("pp", prevPlan);
            cmd.Parameters.AddWithValue("np", newPlan);
            cmd.Parameters.AddWithValue("c", credit);
            cmd.Parameters.AddWithValue("ch", charge);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id, amount_credited = credit, amount_charged = charge });
        });

        // ── Invoices ──
        group.MapGet("/tenants/{tenantId:guid}/invoices", async (Guid tenantId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM central_platform.invoices WHERE tenant_id = @t ORDER BY created_at DESC LIMIT 100", conn);
            cmd.Parameters.AddWithValue("t", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
