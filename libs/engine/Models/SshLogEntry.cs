using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class SshLogEntry : INotifyPropertyChanged
{
    private int       _id;
    private Guid?     _switchId;
    private string    _hostname   = "";
    private string    _hostIp     = "";
    private DateTime  _startedAt;
    private DateTime? _finishedAt;
    private bool      _success;
    private string    _username   = "";
    private int       _port       = 22;
    private string    _error      = "";
    private string    _rawOutput  = "";
    private int       _configLines;
    private string    _logEntries = "";

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public Guid? SwitchId
    {
        get => _switchId;
        set { _switchId = value; OnPropertyChanged(); }
    }

    public string Hostname
    {
        get => _hostname;
        set { _hostname = value; OnPropertyChanged(); }
    }

    public string HostIp
    {
        get => _hostIp;
        set { _hostIp = value; OnPropertyChanged(); }
    }

    public DateTime StartedAt
    {
        get => _startedAt;
        set { _startedAt = value; OnPropertyChanged(); }
    }

    public DateTime? FinishedAt
    {
        get => _finishedAt;
        set { _finishedAt = value; OnPropertyChanged(); }
    }

    public bool Success
    {
        get => _success;
        set { _success = value; OnPropertyChanged(); }
    }

    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    public int Port
    {
        get => _port;
        set { _port = value; OnPropertyChanged(); }
    }

    public string Error
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); }
    }

    public string RawOutput
    {
        get => _rawOutput;
        set { _rawOutput = value; OnPropertyChanged(); }
    }

    public int ConfigLines
    {
        get => _configLines;
        set { _configLines = value; OnPropertyChanged(); }
    }

    public string LogEntries
    {
        get => _logEntries;
        set { _logEntries = value; OnPropertyChanged(); }
    }

    /// <summary>Duration in seconds, computed from StartedAt/FinishedAt.</summary>
    public string Duration =>
        FinishedAt.HasValue
            ? $"{(FinishedAt.Value - StartedAt).TotalSeconds:F1}s"
            : "—";

    /// <summary>Status icon for grid display.</summary>
    public string StatusIcon => Success ? "✓" : "✗";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
