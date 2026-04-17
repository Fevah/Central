using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class TimeEntry : INotifyPropertyChanged
{
    private int _id;
    private int _taskId;
    private string _taskTitle = "";
    private int _userId;
    private string _userName = "";
    private DateTime _entryDate;
    private decimal _hours;
    private string _activityType = "Development";
    private string _notes = "";
    private DateTime _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int TaskId { get => _taskId; set { _taskId = value; N(); } }
    public string TaskTitle { get => _taskTitle; set { _taskTitle = value; N(); } }
    public int UserId { get => _userId; set { _userId = value; N(); } }
    public string UserName { get => _userName; set { _userName = value; N(); } }
    public DateTime EntryDate { get => _entryDate; set { _entryDate = value; N(); } }
    public decimal Hours { get => _hours; set { _hours = value; N(); } }
    public string ActivityType { get => _activityType; set { _activityType = value; N(); } }
    public string Notes { get => _notes; set { _notes = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    public string EntryDateDisplay => EntryDate.ToString("yyyy-MM-dd");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class ActivityFeedItem
{
    public int Id { get; set; }
    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public string ActionIcon => Action switch
    {
        "created" => "+", "updated" => "~", "commented" => "💬",
        "status_changed" => "→", "assigned" => "👤", "deleted" => "✕", _ => "·"
    };
    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - CreatedAt;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}

public class TaskViewConfig : INotifyPropertyChanged
{
    private int _id;
    private int? _projectId;
    private string _name = "";
    private string _viewType = "Tree";
    private string _configJson = "{}";
    private int? _createdBy;
    private bool _isDefault;
    private string _sharedWith = "";
    private DateTime _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string ViewType { get => _viewType; set { _viewType = value; N(); } }
    public string ConfigJson { get => _configJson; set { _configJson = value; N(); } }
    public int? CreatedBy { get => _createdBy; set { _createdBy = value; N(); } }
    public bool IsDefault { get => _isDefault; set { _isDefault = value; N(); } }
    public string SharedWith { get => _sharedWith; set { _sharedWith = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
