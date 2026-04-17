using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class ConfigVersionEntry : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public DateTime DownloadedAt { get; set; }
    public int VersionNum { get; set; }
    public int LineCount { get; set; }
    public string DiffStatus { get; set; } = "";
    public string Operator { get; set; } = "";
    public string SourceIp { get; set; } = "";

    public string DisplayDate => DownloadedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public string DisplayVersion => $"v{VersionNum}";
    public string DisplaySummary => $"v{VersionNum}  ·  {LineCount} lines  ·  {DiffStatus}";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
