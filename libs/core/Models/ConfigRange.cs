using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class ConfigRange : INotifyPropertyChanged
{
    private int    _id;
    private string _category    = "";
    private string _name        = "";
    private string _rangeStart  = "";
    private string _rangeEnd    = "";
    private string _description = "";
    private int    _sortOrder;

    public int Id
    {
        get => _id;
        set { _id = value; OnPropertyChanged(); }
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string RangeStart
    {
        get => _rangeStart;
        set { _rangeStart = value; OnPropertyChanged(); }
    }

    public string RangeEnd
    {
        get => _rangeEnd;
        set { _rangeEnd = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set { _sortOrder = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
