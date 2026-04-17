using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

/// <summary>Configurable Kanban column for a project board.</summary>
public class BoardColumn : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private string _boardName = "Default";
    private string _columnName = "";
    private string _statusMapping = "";
    private int _sortOrder;
    private int? _wipLimit;
    private string _color = "";
    private int _currentCount; // transient — calculated at load time

    public int Id { get => _id; set { _id = value; N(); } }
    public int ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string BoardName { get => _boardName; set { _boardName = value; N(); } }
    public string ColumnName { get => _columnName; set { _columnName = value; N(); } }
    public string StatusMapping { get => _statusMapping; set { _statusMapping = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public int? WipLimit { get => _wipLimit; set { _wipLimit = value; N(); N(nameof(IsOverWip)); N(nameof(WipDisplay)); } }
    public string Color { get => _color; set { _color = value; N(); } }
    public int CurrentCount { get => _currentCount; set { _currentCount = value; N(); N(nameof(IsOverWip)); N(nameof(WipDisplay)); } }

    public bool IsOverWip => WipLimit.HasValue && CurrentCount > WipLimit.Value;
    public string WipDisplay => WipLimit.HasValue ? $"{CurrentCount}/{WipLimit}" : $"{CurrentCount}";
    public string HeaderDisplay => WipLimit.HasValue ? $"{ColumnName} ({CurrentCount}/{WipLimit})" : $"{ColumnName} ({CurrentCount})";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Swim-lane definition for a project board.</summary>
public class BoardLane : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private string _boardName = "Default";
    private string _laneName = "";
    private string _laneField = "";
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; N(); } }
    public int ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string BoardName { get => _boardName; set { _boardName = value; N(); } }
    public string LaneName { get => _laneName; set { _laneName = value; N(); } }
    public string LaneField { get => _laneField; set { _laneField = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
