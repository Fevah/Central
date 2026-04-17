using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Central.Api.Hubs;
using Central.Persistence;

namespace Central.Api.Services;

/// <summary>
/// Background service that listens to PostgreSQL LISTEN/NOTIFY on 'data_changed' channel
/// and broadcasts changes to all SignalR clients via NotificationHub.
/// </summary>
public class ChangeNotifierService : BackgroundService
{
    private readonly DbConnectionFactory _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<ChangeNotifierService> _logger;

    public ChangeNotifierService(DbConnectionFactory db, IHubContext<NotificationHub> hub, ILogger<ChangeNotifierService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_db.ConnectionString);
                await conn.OpenAsync(ct);
                await using var cmd = new NpgsqlCommand("LISTEN data_changed", conn);
                await cmd.ExecuteNonQueryAsync(ct);

                _logger.LogInformation("ChangeNotifier: listening on pg_notify 'data_changed'");

                conn.Notification += async (_, e) =>
                {
                    try
                    {
                        // Parse payload: {"table":"...", "op":"...", "id":"...", "at":"..."}
                        var payload = JsonSerializer.Deserialize<JsonElement>(e.Payload);
                        var table = payload.GetProperty("table").GetString() ?? "";
                        var op = payload.GetProperty("op").GetString() ?? "";
                        var id = payload.GetProperty("id").GetString() ?? "";

                        _logger.LogDebug("ChangeNotifier: {Op} on {Table} id={Id}", op, table, id);

                        // Broadcast to all connected SignalR clients
                        await _hub.Clients.All.SendAsync("DataChanged", table, op, id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ChangeNotifier: failed to process notification");
                    }
                };

                // Wait for notifications — this blocks until connection drops or cancellation
                while (!ct.IsCancellationRequested)
                    await conn.WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ChangeNotifier: connection lost, reconnecting in 5s");
                await Task.Delay(5000, ct);
            }
        }
    }
}
