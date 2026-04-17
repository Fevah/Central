using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class TaskItem : INotifyPropertyChanged
{
    private int _id;
    private int? _parentId;
    private string _title = "";
    private string _description = "";
    private string _status = "Open";
    private string _priority = "Medium";
    private string _taskType = "Task";
    private int? _assignedTo;
    private string _assignedToName = "";
    private int? _createdBy;
    private string _createdByName = "";
    private string _building = "";
    private DateTime? _dueDate;
    private decimal? _estimatedHours;
    private decimal? _actualHours;
    private string _tags = "";
    private int _sortOrder;
    private DateTime _createdAt;
    private DateTime _updatedAt;
    private DateTime? _completedAt;

    // ── Phase 1 new fields ──
    private int? _projectId;
    private string _projectName = "";
    private int? _sprintId;
    private string _sprintName = "";
    private string _wbs = "";
    private bool _isEpic;
    private bool _isUserStory;
    private string _userStory = "";
    private string _detailedDescription = "";
    private string _color = "";
    private string _hyperlink = "";
    private decimal? _points;
    private decimal? _workRemaining;
    private decimal? _budgetedWork;
    private DateTime? _startDate;
    private DateTime? _finishDate;
    private bool _isMilestone;
    private string _risk = "";
    private string _confidence = "";
    private string _severity = "";
    private string _bugPriority = "";
    private int _backlogPriority;
    private int _sprintPriority;
    private int? _committedTo;
    private string _committedToName = "";
    private string _category = "";
    private string _boardColumn = "";
    private string _boardLane = "";
    private decimal _timeSpent;

    // ── Original properties ──
    public int Id { get => _id; set { _id = value; N(); } }
    public int? ParentId { get => _parentId; set { _parentId = value; N(); } }
    public string Title { get => _title; set { _title = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); N(nameof(StatusIcon)); N(nameof(StatusColor)); N(nameof(IsComplete)); } }
    public string Priority { get => _priority; set { _priority = value; N(); N(nameof(PriorityIcon)); N(nameof(PriorityColor)); } }
    public string TaskType { get => _taskType; set { _taskType = value; N(); N(nameof(TypeIcon)); } }
    public int? AssignedTo { get => _assignedTo; set { _assignedTo = value; N(); } }
    public string AssignedToName { get => _assignedToName; set { _assignedToName = value; N(); } }
    public int? CreatedBy { get => _createdBy; set { _createdBy = value; N(); } }
    public string CreatedByName { get => _createdByName; set { _createdByName = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public DateTime? DueDate { get => _dueDate; set { _dueDate = value; N(); N(nameof(IsOverdue)); } }
    public decimal? EstimatedHours { get => _estimatedHours; set { _estimatedHours = value; N(); } }
    public decimal? ActualHours { get => _actualHours; set { _actualHours = value; N(); } }
    public string Tags { get => _tags; set { _tags = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }
    public DateTime? CompletedAt { get => _completedAt; set { _completedAt = value; N(); } }

    // ── Phase 1 new properties ──
    public int? ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string ProjectName { get => _projectName; set { _projectName = value; N(); } }
    public int? SprintId { get => _sprintId; set { _sprintId = value; N(); } }
    public string SprintName { get => _sprintName; set { _sprintName = value; N(); } }
    public string Wbs { get => _wbs; set { _wbs = value; N(); } }
    public bool IsEpic { get => _isEpic; set { _isEpic = value; N(); } }
    public bool IsUserStory { get => _isUserStory; set { _isUserStory = value; N(); } }
    public string UserStory { get => _userStory; set { _userStory = value; N(); } }
    public string DetailedDescription { get => _detailedDescription; set { _detailedDescription = value; N(); } }
    public string Color { get => _color; set { _color = value; N(); } }
    public string Hyperlink { get => _hyperlink; set { _hyperlink = value; N(); } }
    public decimal? Points { get => _points; set { _points = value; N(); } }
    public decimal? WorkRemaining { get => _workRemaining; set { _workRemaining = value; N(); } }
    public decimal? BudgetedWork { get => _budgetedWork; set { _budgetedWork = value; N(); } }
    public DateTime? StartDate { get => _startDate; set { _startDate = value; N(); N(nameof(StartDateDisplay)); } }
    public DateTime? FinishDate { get => _finishDate; set { _finishDate = value; N(); N(nameof(FinishDateDisplay)); } }
    public bool IsMilestone { get => _isMilestone; set { _isMilestone = value; N(); } }
    public string Risk { get => _risk; set { _risk = value; N(); N(nameof(RiskColor)); } }
    public string Confidence { get => _confidence; set { _confidence = value; N(); } }
    public string Severity { get => _severity; set { _severity = value; N(); N(nameof(SeverityColor)); } }
    public string BugPriority { get => _bugPriority; set { _bugPriority = value; N(); } }
    public int BacklogPriority { get => _backlogPriority; set { _backlogPriority = value; N(); } }
    public int SprintPriority { get => _sprintPriority; set { _sprintPriority = value; N(); } }
    public int? CommittedTo { get => _committedTo; set { _committedTo = value; N(); } }
    public string CommittedToName { get => _committedToName; set { _committedToName = value; N(); } }
    public string Category { get => _category; set { _category = value; N(); } }
    public string BoardColumn { get => _boardColumn; set { _boardColumn = value; N(); } }
    public string BoardLane { get => _boardLane; set { _boardLane = value; N(); } }
    public decimal TimeSpent { get => _timeSpent; set { _timeSpent = value; N(); } }

    // ── Computed display properties ──

    public string StatusIcon => Status switch
    {
        "Open" => "○", "InProgress" => "◐", "Review" => "◑",
        "Done" => "●", "Blocked" => "✕", _ => "○"
    };

    public string StatusColor => Status switch
    {
        "Open" => "#9CA3AF", "InProgress" => "#3B82F6", "Review" => "#F59E0B",
        "Done" => "#22C55E", "Blocked" => "#EF4444", _ => "#9CA3AF"
    };

    public string PriorityIcon => Priority switch
    {
        "Critical" => "▲▲", "High" => "▲", "Medium" => "─", "Low" => "▽", _ => "─"
    };

    public string PriorityColor => Priority switch
    {
        "Critical" => "#EF4444", "High" => "#F59E0B", "Medium" => "#9CA3AF", "Low" => "#3B82F6", _ => "#9CA3AF"
    };

    public string TypeIcon => TaskType switch
    {
        "Epic" => "⚡", "Story" => "📖", "Task" => "✓", "Bug" => "🐛",
        "SubTask" => "  ↳", "Milestone" => "◆", _ => "✓"
    };

    public string RiskColor => Risk switch
    {
        "Critical" => "#EF4444", "High" => "#F59E0B", "Medium" => "#FBBF24",
        "Low" => "#22C55E", _ => "#9CA3AF"
    };

    public string SeverityColor => Severity switch
    {
        "Blocker" => "#EF4444", "Critical" => "#EF4444", "Major" => "#F59E0B",
        "Minor" => "#FBBF24", "Cosmetic" => "#9CA3AF", _ => "#9CA3AF"
    };

    public bool IsComplete => Status == "Done";
    public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.Today && !IsComplete;

    public string DueDateDisplay => DueDate?.ToString("yyyy-MM-dd") ?? "";
    public string StartDateDisplay => StartDate?.ToString("yyyy-MM-dd") ?? "";
    public string FinishDateDisplay => FinishDate?.ToString("yyyy-MM-dd") ?? "";
    public string ProgressDisplay => EstimatedHours > 0
        ? $"{ActualHours ?? 0}/{EstimatedHours}h"
        : ActualHours > 0 ? $"{ActualHours}h" : "";
    public string PointsDisplay => Points.HasValue ? $"{Points}pts" : "";

    // ── Gantt computed properties ──
    public double ProgressPercent => Status == "Done" ? 100 :
        (EstimatedHours > 0 && ActualHours > 0 ? Math.Min(99, (double)(ActualHours.Value / EstimatedHours.Value * 100)) : 0);

    /// <summary>Baseline dates — populated from task_baselines when loaded.</summary>
    public DateTime? BaselineStartDate { get; set; }
    public DateTime? BaselineFinishDate { get; set; }

    // ── Custom column values (populated at load time) ──
    public Dictionary<string, string> CustomValues { get; set; } = new();

    // ── Children (for tree) ──
    public ObservableCollection<TaskItem> Children { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class TaskComment
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public int? UserId { get; set; }
    public string UserName { get; set; } = "";
    public string CommentText { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
