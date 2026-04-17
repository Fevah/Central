namespace Central.Engine.Services;

/// <summary>
/// Engine alert service — monitors events and raises alerts.
/// Alerts are logged to DB and shown as toast notifications.
/// Phase 6.5: Ping failure, SSH failure, BGP peer down.
/// Phase 6.6: Config drift detection.
/// </summary>
public sealed class AlertService
{
    public static AlertService Instance { get; } = new();

    public event Action<Alert>? AlertRaised;

    /// <summary>Recent alerts (last 100).</summary>
    public List<Alert> Recent { get; } = new();

    /// <summary>Raise a ping failure alert.</summary>
    public void PingFailed(string hostname, string? ip)
    {
        Raise(new Alert
        {
            Severity = AlertSeverity.Warning,
            Category = "Ping",
            Title = $"Ping failed: {hostname}",
            Detail = $"Switch {hostname} ({ip ?? "no IP"}) is unreachable",
            Hostname = hostname
        });
    }

    /// <summary>Raise a ping recovery alert.</summary>
    public void PingRecovered(string hostname, double latencyMs)
    {
        Raise(new Alert
        {
            Severity = AlertSeverity.Info,
            Category = "Ping",
            Title = $"Ping recovered: {hostname}",
            Detail = $"Switch {hostname} is back online ({latencyMs:F0}ms)",
            Hostname = hostname
        });
    }

    /// <summary>Raise an SSH failure alert.</summary>
    public void SshFailed(string hostname, string error)
    {
        Raise(new Alert
        {
            Severity = AlertSeverity.Error,
            Category = "SSH",
            Title = $"SSH failed: {hostname}",
            Detail = error,
            Hostname = hostname
        });
    }

    /// <summary>Raise a config drift alert.</summary>
    public void ConfigDrift(string hostname, int changedLines)
    {
        Raise(new Alert
        {
            Severity = AlertSeverity.Warning,
            Category = "Config",
            Title = $"Config drift: {hostname}",
            Detail = $"{changedLines} lines changed since last backup",
            Hostname = hostname
        });
    }

    /// <summary>Raise a BGP peer down alert.</summary>
    public void BgpPeerDown(string hostname, string neighborIp, string remoteAs)
    {
        Raise(new Alert
        {
            Severity = AlertSeverity.Error,
            Category = "BGP",
            Title = $"BGP peer down: {hostname} → {neighborIp}",
            Detail = $"Neighbor {neighborIp} (AS {remoteAs}) is not established",
            Hostname = hostname
        });
    }

    private void Raise(Alert alert)
    {
        Recent.Insert(0, alert);
        if (Recent.Count > 100) Recent.RemoveAt(100);

        // Show toast
        var notify = NotificationService.Instance;
        switch (alert.Severity)
        {
            case AlertSeverity.Error:   notify.Error(alert.Title, alert.Detail); break;
            case AlertSeverity.Warning: notify.Warning(alert.Title, alert.Detail); break;
            case AlertSeverity.Info:    notify.Info(alert.Title, alert.Detail); break;
        }

        AlertRaised?.Invoke(alert);
    }
}

public class Alert
{
    public AlertSeverity Severity { get; set; }
    public string Category { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string? Hostname { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum AlertSeverity { Info, Warning, Error }
