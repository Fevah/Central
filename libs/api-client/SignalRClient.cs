using Microsoft.AspNetCore.SignalR.Client;

namespace Central.ApiClient;

/// <summary>
/// SignalR client that connects to the NotificationHub and raises events
/// when data changes are broadcast from the API server.
/// </summary>
public class SignalRClient : IAsyncDisposable
{
    private HubConnection? _connection;

    /// <summary>Fired when any table has data changes. Args: table, operation, id.</summary>
    public event Action<string, string, string>? DataChanged;

    /// <summary>Fired when a ping result is broadcast. Args: hostname, success, latencyMs.</summary>
    public event Action<string, bool, double>? PingResult;

    /// <summary>Fired when sync progress updates. Args: hostname, status, progress (0-100).</summary>
    public event Action<string, string, int>? SyncProgress;

    /// <summary>Fires when config drift is detected. Args: hostname, changedLines.</summary>
    public event Action<string, int>? ConfigDrift;

    /// <summary>Fired when a user starts editing an entity. Args: entityType, entityId, username.</summary>
    public event Action<string, string, string>? EditorJoined;

    /// <summary>Fired when a user stops editing an entity. Args: entityType, entityId, username.</summary>
    public event Action<string, string, string>? EditorLeft;

    /// <summary>True if connected to the hub.</summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>Connect to the SignalR hub with JWT auth.</summary>
    public async Task ConnectAsync(string hubUrl, string jwtToken)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, o => o.AccessTokenProvider = () => Task.FromResult<string?>(jwtToken))
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
            .Build();

        _connection.On<string, string, string>("DataChanged", (table, op, id) =>
            DataChanged?.Invoke(table, op, id));

        _connection.On<string, bool, double>("PingResult", (host, ok, ms) =>
            PingResult?.Invoke(host, ok, ms));

        _connection.On<string, string, int>("SyncProgress", (host, status, pct) =>
            SyncProgress?.Invoke(host, status, pct));

        _connection.On<string, int>("ConfigDrift", (host, changedLines) =>
            ConfigDrift?.Invoke(host, changedLines));

        _connection.On<string, string, string>("EditorJoined", (type, id, user) =>
            EditorJoined?.Invoke(type, id, user));

        _connection.On<string, string, string>("EditorLeft", (type, id, user) =>
            EditorLeft?.Invoke(type, id, user));

        await _connection.StartAsync();
    }

    /// <summary>Disconnect from the hub.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
