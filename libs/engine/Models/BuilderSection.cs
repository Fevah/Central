using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public record ConfigLine(string Text, string SectionKey);

public class BuilderSection : INotifyPropertyChanged
{
    private string _key = "";
    private string _displayName = "";
    private bool _isEnabled = true;
    private string _colorHex = "#888888";
    private int _lineCount;

    public string Key { get => _key; set { _key = value; N(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); } }
    public string ColorHex { get => _colorHex; set { _colorHex = value; N(); } }
    public int LineCount { get => _lineCount; set { _lineCount = value; N(); } }
    public ObservableCollection<BuilderItem> Items { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class BuilderItem : INotifyPropertyChanged
{
    private string _key = "";
    private string _displayText = "";
    private bool _isEnabled = true;

    public string Key { get => _key; set { _key = value; N(); } }
    public string DisplayText { get => _displayText; set { _displayText = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
