using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class ContainerInfo : INotifyPropertyChanged
{
    private string _name = "";
    private string _image = "";
    private string _status = "";
    private string _state = "";
    private string _created = "";
    private string _ports = "";
    private string _cpuPercent = "";
    private string _memUsage = "";

    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public string Image { get => _image; set { _image = value; OnPropertyChanged(); } }
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public string State { get => _state; set { _state = value; OnPropertyChanged(); } }
    public string Created { get => _created; set { _created = value; OnPropertyChanged(); } }
    public string Ports { get => _ports; set { _ports = value; OnPropertyChanged(); } }
    public string CpuPercent { get => _cpuPercent; set { _cpuPercent = value; OnPropertyChanged(); } }
    public string MemUsage { get => _memUsage; set { _memUsage = value; OnPropertyChanged(); } }

    /// <summary>Row colour based on container state.</summary>
    public string StateColor => State switch
    {
        "running" => "#22C55E",
        "exited"  => "#EF4444",
        "paused"  => "#F59E0B",
        _         => "#6B7280"
    };

    public bool IsRunning => State == "running";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
