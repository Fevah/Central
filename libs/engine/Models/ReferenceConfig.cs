using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class ReferenceConfig : INotifyPropertyChanged
{
    private int _id;
    private string _entityType = "";
    private string _prefix = "";
    private string _suffix = "";
    private int _padLength = 6;
    private long _nextValue = 1;
    private string _description = "";

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string EntityType { get => _entityType; set { _entityType = value; OnPropertyChanged(); } }
    public string Prefix { get => _prefix; set { _prefix = value; OnPropertyChanged(); OnPropertyChanged(nameof(SampleOutput)); } }
    public string Suffix { get => _suffix; set { _suffix = value; OnPropertyChanged(); OnPropertyChanged(nameof(SampleOutput)); } }
    public int PadLength { get => _padLength; set { _padLength = value; OnPropertyChanged(); OnPropertyChanged(nameof(SampleOutput)); } }
    public long NextValue { get => _nextValue; set { _nextValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(SampleOutput)); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }

    /// <summary>Live preview of what the next reference number looks like.</summary>
    public string SampleOutput => $"{Prefix}{NextValue.ToString().PadLeft(PadLength, '0')}{Suffix}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
