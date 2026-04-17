using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Central.Api.Hubs;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>
/// Webhook receiver for inbound integrations.
/// External systems POST JSON to /api/webhooks/{source} and Central processes it.
/// Validates HMAC-SHA256 signature via X-Webhook-Signature header when CENTRAL_WEBHOOK_SECRET is set.
/// </summary>
public static class WebhookEndpoints
{
    private static readonly byte[]? WebhookSecret = GetWebhookSecret();

    private static byte[]? GetWebhookSecret()
    {
        var secret = Environment.GetEnvironmentVariable("CENTRAL_WEBHOOK_SECRET");
        return string.IsNullOrEmpty(secret) ? null : Encoding.UTF8.GetBytes(secret);
    }

    public static RouteGroupBuilder MapWebhookEndpoints(this RouteGroupBuilder group)
    {
        // Generic webhook receiver — stores payload and optionally triggers sync
        group.MapPost("/{source}", async (string source, HttpContext ctx, DbConnectionFactory db, IHubContext<NotificationHub> hub) =>
        {
            string body;
            using (var reader = new StreamReader(ctx.Request.Body))
                body = await reader.ReadToEndAsync();

            // Validate HMAC-SHA256 signature if webhook secret is configured
            if (WebhookSecret != null)
            {
                var signature = ctx.Request.Headers["X-Webhook-Signature"].FirstOrDefault();
                if (string.IsNullOrEmpty(signature))
                    return Results.Json(new { error = "Missing X-Webhook-Signature header" }, statusCode: 401);

                var expectedHash = ComputeHmac(body);
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signature),
                    Encoding.UTF8.GetBytes(expectedHash)))
                {
                    return Results.Json(new { error = "Invalid webhook signature" }, statusCode: 401);
                }
            }

            // Log the webhook
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                @"INSERT INTO webhook_log (source, headers, payload, received_at)
                  VALUES (@s, @h, @p::jsonb, NOW()) RETURNING id", conn);
            cmd.Parameters.AddWithValue("s", source);
            cmd.Parameters.AddWithValue("h", JsonSerializer.Serialize(
                ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())));

            // Validate JSON — store as-is or wrap in object
            try { JsonDocument.Parse(body); cmd.Parameters.AddWithValue("p", body); }
            catch { cmd.Parameters.AddWithValue("p", JsonSerializer.Serialize(new { raw = body })); }

            var id = (long)(await cmd.ExecuteScalarAsync())!;

            // Broadcast via SignalR so desktop clients can react
            await hub.Clients.All.SendAsync("WebhookReceived", source, id);

            // Auto-trigger sync if a sync_config matches this source
            try
            {
                await using var syncCmd = new Npgsql.NpgsqlCommand(
                    "SELECT id FROM sync_configs WHERE agent_type = @s AND is_enabled = true LIMIT 1", conn);
                syncCmd.Parameters.AddWithValue("s", source);
                var syncConfigId = await syncCmd.ExecuteScalarAsync();
                if (syncConfigId is int configId)
                {
                    await using var markCmd = new Npgsql.NpgsqlCommand(
                        "UPDATE sync_configs SET last_sync_status = 'pending' WHERE id = @id", conn);
                    markCmd.Parameters.AddWithValue("id", configId);
                    await markCmd.ExecuteNonQueryAsync();
                }
            }
            catch { }

            return Results.Ok(new { id, source, status = "received" });
        });

        // List recent webhooks (authenticated — requires auth)
        group.MapGet("/", async (DbConnectionFactory db, int? limit) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT id, source, received_at, processed FROM webhook_log ORDER BY received_at DESC LIMIT @limit", conn);
            cmd.Parameters.AddWithValue("limit", Math.Min(limit ?? 50, 500));
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        }).RequireAuthorization();

        // Get webhook payload (authenticated)
        group.MapGet("/{id:long}/payload", async (long id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT source, headers, payload::text, received_at FROM webhook_log WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            return Results.Ok(new
            {
                source = r.GetString(0),
                headers = r.GetString(1),
                payload = r.GetString(2),
                received_at = r.GetDateTime(3)
            });
        }).RequireAuthorization();

        return group;
    }

    private static string ComputeHmac(string payload)
    {
        using var hmac = new HMACSHA256(WebhookSecret!);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
