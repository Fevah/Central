using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Central.Desktop.Services;

/// <summary>
/// Manages database connectivity with timeouts and background retry.
/// Ensures the app never freezes waiting for a dead database.
/// </summary>
public class ConnectivityManager : INotifyPropertyChanged, IDisposable
{
    private readonly string _connectionString;
    private readonly int _connectTimeoutSeconds;
    private System.Threading.Timer? _retryTimer;
    private bool _disposed;

    public ConnectivityManager(string connectionString, int connectTimeoutSeconds = 5)
    {
        _connectionString = connectionString;
        _connectTimeoutSeconds = connectTimeoutSeconds;
    }

    // ── Observable state ──────────────────────────────────────────────────

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set { if (_isConnected != value) { _isConnected = value; OnPropertyChanged(); ConnectionChanged?.Invoke(this, value); } }
    }

    private string _status = "Offline";
    public string Status
    {
        get => _status;
        private set { if (_status != value) { _status = value; OnPropertyChanged(); } }
    }

    private string _statusColor = "Red";
    public string StatusColor
    {
        get => _statusColor;
        private set { if (_statusColor != value) { _statusColor = value; OnPropertyChanged(); } }
    }

    private string _statusTooltip = "Not connected";
    public string StatusTooltip
    {
        get => _statusTooltip;
        private set { if (_statusTooltip != value) { _statusTooltip = value; OnPropertyChanged(); } }
    }

    /// <summary>Fires when connection state changes (true = connected).</summary>
    public event EventHandler<bool>? ConnectionChanged;

    // ── Core operations ───────────────────────────────────────────────────

    /// <summary>Test DB connectivity with a short timeout. Returns true if reachable.</summary>
    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        StatusColor = "Yellow";
        Status = "Checking…";
        StatusTooltip = "Testing database connection…";

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString)
            {
                Timeout = _connectTimeoutSeconds,
                CommandTimeout = _connectTimeoutSeconds
            };

            await using var conn = new NpgsqlConnection(builder.ToString());

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_connectTimeoutSeconds + 2));

            await conn.OpenAsync(cts.Token);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(cts.Token);

            IsConnected = true;
            StatusColor = "Green";
            Status = "Connected";
            StatusTooltip = $"Database connected · {DateTime.Now:HH:mm:ss}";
            return true;
        }
        catch (OperationCanceledException)
        {
            IsConnected = false;
            StatusColor = "Red";
            Status = "Timeout";
            StatusTooltip = $"Connection timed out after {_connectTimeoutSeconds}s";
            return false;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusColor = "Red";
            Status = "Offline";
            StatusTooltip = $"Connection failed: {ex.Message}";
            return false;
        }
    }

    /// <summary>Start background retry loop. Fires ConnectionChanged when DB comes online.</summary>
    public void StartRetryLoop(int intervalSeconds = 10)
    {
        StopRetryLoop();
        _retryTimer = new System.Threading.Timer(async _ =>
        {
            if (_disposed) return;
            var was = IsConnected;
            await TestConnectionAsync();
            // Stop retrying once connected
            if (!was && IsConnected)
                StopRetryLoop();
        }, null, TimeSpan.FromSeconds(intervalSeconds), TimeSpan.FromSeconds(intervalSeconds));
    }

    /// <summary>Stop the background retry loop.</summary>
    public void StopRetryLoop()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    /// <summary>Run an async DB operation with a timeout wrapper. Returns default(T) on failure.</summary>
    public async Task<T?> RunWithTimeoutAsync<T>(Func<Task<T>> operation, int timeoutSeconds = 10, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            IsConnected = false;
            StatusColor = "Red";
            Status = "Timeout";
            StatusTooltip = $"Operation timed out after {timeoutSeconds}s";
            return default;
        }
        catch (NpgsqlException ex)
        {
            IsConnected = false;
            StatusColor = "Red";
            Status = "Error";
            StatusTooltip = $"DB error: {ex.Message}";
            return default;
        }
    }

    // ── Data Service Mode ─────────────────────────────────────────────────

    private Central.Core.Data.DataServiceMode _mode = Central.Core.Data.DataServiceMode.DirectDb;
    /// <summary>Current data service mode (DirectDb, Api, or Offline).</summary>
    public Central.Core.Data.DataServiceMode Mode
    {
        get => _mode;
        set { if (_mode != value) { _mode = value; OnPropertyChanged(); } }
    }

    /// <summary>API server URL (e.g., http://localhost:5000). Set to enable Api mode.</summary>
    public string? ApiUrl { get; set; }

    /// <summary>The active IDataService based on current mode. Use this for all data operations.</summary>
    public Central.Core.Data.IDataService? ActiveDataService { get; private set; }

    private Central.Core.Data.IDataService? _directDbService;
    private Central.Core.Data.IDataService? _apiService;

    /// <summary>Register the direct DB data service (created at startup with DbRepository).</summary>
    public void RegisterDirectDb(Central.Core.Data.IDataService service) => _directDbService = service;

    /// <summary>Register the API data service (created when API URL is configured).</summary>
    public void RegisterApi(Central.Core.Data.IDataService service) => _apiService = service;

    /// <summary>Switch to the specified mode and update ActiveDataService.</summary>
    public void SwitchMode(Central.Core.Data.DataServiceMode newMode)
    {
        Mode = newMode;
        ActiveDataService = newMode switch
        {
            Central.Core.Data.DataServiceMode.Api => _apiService ?? _directDbService,
            Central.Core.Data.DataServiceMode.DirectDb => _directDbService,
            Central.Core.Data.DataServiceMode.Offline => null,
            _ => _directDbService
        };
        OnPropertyChanged(nameof(ActiveDataService));
    }

    // ── SignalR real-time updates ────────────────────────────────────────

    private Central.Api.Client.SignalRClient? _signalR;

    /// <summary>Fires when a table is changed on the server. Args: table, operation, id.</summary>
    public event Action<string, string, string>? DataChanged;

    /// <summary>Connect to the API's SignalR hub for real-time change notifications.</summary>
    public async Task ConnectSignalRAsync(string hubUrl, string jwtToken)
    {
        _signalR = new Central.Api.Client.SignalRClient();
        _signalR.DataChanged += (table, op, id) => DataChanged?.Invoke(table, op, id);
        try
        {
            await _signalR.ConnectAsync(hubUrl, jwtToken);
        }
        catch (Exception ex)
        {
            StatusTooltip = $"SignalR failed: {ex.Message}";
        }
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _disposed = true;
        StopRetryLoop();
        _signalR?.DisposeAsync().AsTask().Wait(1000);
    }
}
