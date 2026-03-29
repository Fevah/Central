using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class LookupItem : INotifyPropertyChanged
{
    private int    _id;
    private string _category  = "";
    private string _value     = "";
    private int    _sortOrder;
    private string _gridName  = "";
    private string _module    = "";

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

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(); }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set { _sortOrder = value; OnPropertyChanged(); }
    }

    public string GridName
    {
        get => _gridName;
        set { _gridName = value; OnPropertyChanged(); }
    }

    public string Module
    {
        get => _module;
        set { _module = value; OnPropertyChanged(); }
    }

    // TreeList requires ParentId — null for all items (flat list)
    public int? ParentId => null;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
