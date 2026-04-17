using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Central.Engine.Models;

/// <summary>Saved report definition with visual query builder config.</summary>
public class SavedReport : INotifyPropertyChanged
{
    private int _id;
    private int? _projectId;
    private string _name = "";
    private string _folder = "";
    private string _queryJson = "{}";
    private int? _createdBy;
    private string _createdByName = "";
    private string _sharedWith = "";
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Folder { get => _folder; set { _folder = value; N(); } }
    public string QueryJson { get => _queryJson; set { _queryJson = value; N(); } }
    public int? CreatedBy { get => _createdBy; set { _createdBy = value; N(); } }
    public string CreatedByName { get => _createdByName; set { _createdByName = value; N(); } }
    public string SharedWith { get => _sharedWith; set { _sharedWith = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    public string DisplayPath => string.IsNullOrEmpty(Folder) ? Name : $"{Folder}/{Name}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Query filter condition for the visual query builder.</summary>
public class ReportFilter
{
    public string Field { get; set; } = "";
    public string Operator { get; set; } = "="; // =, !=, >, <, >=, <=, contains, between, in, isNull, isNotNull
    public string Value { get; set; } = "";
    public string? Value2 { get; set; }  // for 'between'
    public string Logic { get; set; } = "AND"; // AND, OR
}

/// <summary>Query definition parsed from QueryJson.</summary>
public class ReportQuery
{
    public List<string> Columns { get; set; } = [];
    public List<ReportFilter> Filters { get; set; } = [];
    public string SortField { get; set; } = "";
    public string SortDirection { get; set; } = "ASC";
    public string GroupField { get; set; } = "";
    public string EntityType { get; set; } = "task"; // task, device, switch, etc.
}

/// <summary>Dashboard with configurable tile layout.</summary>
public class Dashboard : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _layoutJson = "{}";
    private string _template = "";
    private int? _createdBy;
    private string _createdByName = "";
    private string _sharedWith = "";
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string LayoutJson { get => _layoutJson; set { _layoutJson = value; N(); } }
    public string Template { get => _template; set { _template = value; N(); } }
    public int? CreatedBy { get => _createdBy; set { _createdBy = value; N(); } }
    public string CreatedByName { get => _createdByName; set { _createdByName = value; N(); } }
    public string SharedWith { get => _sharedWith; set { _sharedWith = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Dashboard tile configuration.</summary>
public class DashboardTile
{
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColSpan { get; set; } = 1;
    public string ChartType { get; set; } = "Bar"; // Bar, Pie, Line, XY, TrafficLight, Text, Burndown
    public string Title { get; set; } = "";
    public string DataSource { get; set; } = ""; // report name or inline query
    public string ArgumentField { get; set; } = "";
    public string ValueField { get; set; } = "";
    public string Color { get; set; } = "#3B82F6";
}
