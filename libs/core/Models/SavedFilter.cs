using System.ComponentModel;

namespace Central.Core.Models;

/// <summary>Persisted filter expression for a grid panel.</summary>
public class SavedFilter : INotifyPropertyChanged
{
    private int _id;
    private int? _userId;
    private string _panelName = "";
    private string _filterName = "";
    private string _filterExpr = "";
    private bool _isDefault;
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int? UserId { get => _userId; set { _userId = value; OnPropertyChanged(); } }
    public string PanelName { get => _panelName; set { _panelName = value; OnPropertyChanged(); } }
    public string FilterName { get => _filterName; set { _filterName = value; OnPropertyChanged(); } }
    public string FilterExpr { get => _filterExpr; set { _filterExpr = value; OnPropertyChanged(); } }
    public bool IsDefault { get => _isDefault; set { _isDefault = value; OnPropertyChanged(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); } }

    /// <summary>True if this is a shared filter (visible to all users).</summary>
    public bool IsShared => UserId == null;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
