using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Central.Core.Models;

/// <summary>Project-scoped custom field definition.</summary>
public class CustomColumn : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private string _name = "";
    private string _columnType = "Text";
    private string _config = "";
    private int _sortOrder;
    private string _defaultValue = "";
    private bool _isRequired;

    public int Id { get => _id; set { _id = value; N(); } }
    public int ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    /// <summary>Text, RichText, Number, Hours, DropList, Date, DateTime, People, Computed</summary>
    public string ColumnType { get => _columnType; set { _columnType = value; N(); } }
    /// <summary>JSON config: DropList options, Computed formula, aggregation type (Sum/Avg/Min/Max).</summary>
    public string Config { get => _config; set { _config = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public string DefaultValue { get => _defaultValue; set { _defaultValue = value; N(); } }
    public bool IsRequired { get => _isRequired; set { _isRequired = value; N(); } }

    /// <summary>Parse DropList options from JSON config.</summary>
    public string[] GetDropListOptions()
    {
        if (string.IsNullOrEmpty(Config)) return [];
        try
        {
            var doc = JsonDocument.Parse(Config);
            if (doc.RootElement.TryGetProperty("options", out var opts) && opts.ValueKind == JsonValueKind.Array)
                return opts.EnumerateArray().Select(e => e.GetString() ?? "").ToArray();
        }
        catch { }
        return [];
    }

    /// <summary>Get aggregation type from config (Sum, Avg, Min, Max, or null).</summary>
    public string? GetAggregationType()
    {
        if (string.IsNullOrEmpty(Config)) return null;
        try
        {
            var doc = JsonDocument.Parse(Config);
            if (doc.RootElement.TryGetProperty("aggregation", out var agg))
                return agg.GetString();
        }
        catch { }
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Field-level permission for a custom column.</summary>
public class CustomColumnPermission
{
    public int Id { get; set; }
    public int ColumnId { get; set; }
    public int? UserId { get; set; }
    public string GroupName { get; set; } = "";
    public bool CanView { get; set; } = true;
    public bool CanEdit { get; set; } = true;
}

/// <summary>Value of a custom column for a specific task.</summary>
public class TaskCustomValue : INotifyPropertyChanged
{
    private int _taskId;
    private int _columnId;
    private string _columnName = "";
    private string _columnType = "Text";
    private string? _valueText;
    private decimal? _valueNumber;
    private DateTime? _valueDate;
    private string? _valueJson;

    public int TaskId { get => _taskId; set { _taskId = value; N(); } }
    public int ColumnId { get => _columnId; set { _columnId = value; N(); } }
    public string ColumnName { get => _columnName; set { _columnName = value; N(); } }
    public string ColumnType { get => _columnType; set { _columnType = value; N(); } }
    public string? ValueText { get => _valueText; set { _valueText = value; N(); N(nameof(DisplayValue)); } }
    public decimal? ValueNumber { get => _valueNumber; set { _valueNumber = value; N(); N(nameof(DisplayValue)); } }
    public DateTime? ValueDate { get => _valueDate; set { _valueDate = value; N(); N(nameof(DisplayValue)); } }
    public string? ValueJson { get => _valueJson; set { _valueJson = value; N(); N(nameof(DisplayValue)); } }

    /// <summary>Display value based on column type.</summary>
    public string DisplayValue => ColumnType switch
    {
        "Number" or "Hours" => ValueNumber?.ToString("N2") ?? "",
        "Date" => ValueDate?.ToString("yyyy-MM-dd") ?? "",
        "DateTime" => ValueDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
        _ => ValueText ?? ""
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
