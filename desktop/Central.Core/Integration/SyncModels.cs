using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Integration;

/// <summary>Sync configuration — one per external system integration.</summary>
public class SyncConfig : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _agentType = "";
    private bool _isEnabled = true;
    private string _direction = "pull";
    private string _scheduleCron = "";
    private int _intervalMinutes = 60;
    private int _maxConcurrent = 1;
    private string _configJson = "{}";
    private DateTime? _lastSyncAt;
    private string _lastSyncStatus = "never";
    private string? _lastError;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string AgentType { get => _agentType; set { _agentType = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); } }
    public string Direction { get => _direction; set { _direction = value; N(); } }
    public string ScheduleCron { get => _scheduleCron; set { _scheduleCron = value; N(); } }
    public int IntervalMinutes { get => _intervalMinutes; set { _intervalMinutes = value; N(); } }
    public int MaxConcurrent { get => _maxConcurrent; set { _maxConcurrent = value; N(); } }
    public string ConfigJson { get => _configJson; set { _configJson = value; N(); } }
    public DateTime? LastSyncAt { get => _lastSyncAt; set { _lastSyncAt = value; N(); } }
    public string LastSyncStatus { get => _lastSyncStatus; set { _lastSyncStatus = value; N(); } }
    public string? LastError { get => _lastError; set { _lastError = value; N(); } }

    public string StatusColor => LastSyncStatus switch
    {
        "success" => "#22C55E",
        "running" => "#3B82F6",
        "failed" => "#EF4444",
        "partial" => "#F59E0B",
        _ => "#6B7280"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Entity mapping — which source entity maps to which target table.</summary>
public class SyncEntityMap : INotifyPropertyChanged
{
    private int _id;
    private int _syncConfigId;
    private string _sourceEntity = "";
    private string _targetTable = "";
    private string _mappingType = "one_to_one";
    private bool _isEnabled = true;
    private string _syncDirection = "pull";
    private string _filterExpr = "";
    private string _upsertKey = "id";
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; N(); } }
    public int SyncConfigId { get => _syncConfigId; set { _syncConfigId = value; N(); } }
    public string SourceEntity { get => _sourceEntity; set { _sourceEntity = value; N(); } }
    public string TargetTable { get => _targetTable; set { _targetTable = value; N(); } }
    public string MappingType { get => _mappingType; set { _mappingType = value; N(); } }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; N(); } }
    public string SyncDirection { get => _syncDirection; set { _syncDirection = value; N(); } }
    public string FilterExpr { get => _filterExpr; set { _filterExpr = value; N(); } }
    public string UpsertKey { get => _upsertKey; set { _upsertKey = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Field mapping — source field → target column with optional converter.</summary>
public class SyncFieldMap : INotifyPropertyChanged
{
    private int _id;
    private int _entityMapId;
    private string _sourceField = "";
    private string _targetColumn = "";
    private string _converterType = "direct";
    private string _converterExpr = "";
    private bool _isKey;
    private bool _isRequired;
    private string? _defaultValue;
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; N(); } }
    public int EntityMapId { get => _entityMapId; set { _entityMapId = value; N(); } }
    public string SourceField { get => _sourceField; set { _sourceField = value; N(); } }
    public string TargetColumn { get => _targetColumn; set { _targetColumn = value; N(); } }
    public string ConverterType { get => _converterType; set { _converterType = value; N(); } }
    public string ConverterExpr { get => _converterExpr; set { _converterExpr = value; N(); } }
    public bool IsKey { get => _isKey; set { _isKey = value; N(); } }
    public bool IsRequired { get => _isRequired; set { _isRequired = value; N(); } }
    public string? DefaultValue { get => _defaultValue; set { _defaultValue = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Sync execution log entry.</summary>
public class SyncLogEntry
{
    public long Id { get; set; }
    public int SyncConfigId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "running";
    public string? EntityName { get; set; }
    public int RecordsRead { get; set; }
    public int RecordsCreated { get; set; }
    public int RecordsUpdated { get; set; }
    public int RecordsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }

    public string StatusColor => Status switch
    {
        "success" => "#22C55E",
        "running" => "#3B82F6",
        "failed" => "#EF4444",
        _ => "#6B7280"
    };
}
