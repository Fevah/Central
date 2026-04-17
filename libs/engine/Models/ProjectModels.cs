using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

/// <summary>Strategic container — groups programmes/projects by business unit or product line.</summary>
public class Portfolio : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private string _description = "";
    private int? _ownerId;
    private string _ownerName = "";
    private bool _archived;
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public int? OwnerId { get => _ownerId; set { _ownerId = value; N(); } }
    public string OwnerName { get => _ownerName; set { _ownerName = value; N(); } }
    public bool Archived { get => _archived; set { _archived = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    public ObservableCollection<Programme> Programmes { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Cross-project coordination — SAFe ART / Release Train equivalent.</summary>
public class Programme : INotifyPropertyChanged
{
    private int _id;
    private int? _portfolioId;
    private string _name = "";
    private string _description = "";
    private int? _ownerId;
    private string _ownerName = "";
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? PortfolioId { get => _portfolioId; set { _portfolioId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public int? OwnerId { get => _ownerId; set { _ownerId = value; N(); } }
    public string OwnerName { get => _ownerName; set { _ownerName = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    public ObservableCollection<TaskProject> Projects { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Primary work container — has its own backlog, planning, QA views.</summary>
public class TaskProject : INotifyPropertyChanged
{
    private int _id;
    private int? _programmeId;
    private string _name = "";
    private string _description = "";
    private string _schedulingMethod = "FixedDuration";
    private string _defaultMode = "Agile";
    private string _methodTemplate = "Scrum";
    private string _calendar = "";
    private bool _archived;
    private DateTime _createdAt;
    private DateTime _updatedAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int? ProgrammeId { get => _programmeId; set { _programmeId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string SchedulingMethod { get => _schedulingMethod; set { _schedulingMethod = value; N(); } }
    public string DefaultMode { get => _defaultMode; set { _defaultMode = value; N(); } }
    public string MethodTemplate { get => _methodTemplate; set { _methodTemplate = value; N(); } }
    public string Calendar { get => _calendar; set { _calendar = value; N(); } }
    public bool Archived { get => _archived; set { _archived = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; N(); } }

    // Display
    public string DisplayName => Archived ? $"{Name} (archived)" : Name;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Project membership with role.</summary>
public class ProjectMember : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private int _userId;
    private string _userName = "";
    private string _role = "Member";

    public int Id { get => _id; set { _id = value; N(); } }
    public int ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public int UserId { get => _userId; set { _userId = value; N(); } }
    public string UserName { get => _userName; set { _userName = value; N(); } }
    public string Role { get => _role; set { _role = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Time-boxed iteration within a project.</summary>
public class Sprint : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private string _name = "";
    private DateTime? _startDate;
    private DateTime? _endDate;
    private string _goal = "";
    private string _status = "Planning";
    private decimal? _velocityPoints;
    private decimal? _velocityHours;
    private DateTime _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public DateTime? StartDate { get => _startDate; set { _startDate = value; N(); } }
    public DateTime? EndDate { get => _endDate; set { _endDate = value; N(); } }
    public string Goal { get => _goal; set { _goal = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }
    public decimal? VelocityPoints { get => _velocityPoints; set { _velocityPoints = value; N(); } }
    public decimal? VelocityHours { get => _velocityHours; set { _velocityHours = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    // Display
    public string DateRange => StartDate.HasValue && EndDate.HasValue
        ? $"{StartDate:yyyy-MM-dd} → {EndDate:yyyy-MM-dd}" : "";
    public string DisplayName => $"{Name} ({Status})";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Named release with target date.</summary>
public class Release : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private string _name = "";
    private DateTime? _targetDate;
    private string _description = "";
    private string _status = "Planned";
    private DateTime _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int ProjectId { get => _projectId; set { _projectId = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public DateTime? TargetDate { get => _targetDate; set { _targetDate = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Cross-project item link (relates_to, blocks, duplicates).</summary>
public class TaskLink : INotifyPropertyChanged
{
    private int _id;
    private int _sourceId;
    private int _targetId;
    private string _linkType = "relates_to";
    private int _lagDays;
    private string _targetTitle = "";
    private DateTime _createdAt;

    public int Id { get => _id; set { _id = value; N(); } }
    public int SourceId { get => _sourceId; set { _sourceId = value; N(); } }
    public int TargetId { get => _targetId; set { _targetId = value; N(); } }
    public string LinkType { get => _linkType; set { _linkType = value; N(); } }
    public int LagDays { get => _lagDays; set { _lagDays = value; N(); } }
    public string TargetTitle { get => _targetTitle; set { _targetTitle = value; N(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }

    public string LinkDisplay => $"{LinkType}: {TargetTitle}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Gantt dependency link (FS/FF/SF/SS with optional lag).</summary>
public class TaskDependency : INotifyPropertyChanged
{
    private int _id;
    private int _predecessorId;
    private int _successorId;
    private string _depType = "FS";
    private int _lagDays;
    private string _predecessorTitle = "";
    private string _successorTitle = "";

    public int Id { get => _id; set { _id = value; N(); } }
    public int PredecessorId { get => _predecessorId; set { _predecessorId = value; N(); } }
    public int SuccessorId { get => _successorId; set { _successorId = value; N(); } }
    public string DepType { get => _depType; set { _depType = value; N(); } }
    public int LagDays { get => _lagDays; set { _lagDays = value; N(); } }
    public string PredecessorTitle { get => _predecessorTitle; set { _predecessorTitle = value; N(); } }
    public string SuccessorTitle { get => _successorTitle; set { _successorTitle = value; N(); } }

    public string DepDisplay => $"{DepType}{(LagDays != 0 ? $"+{LagDays}d" : "")}: {PredecessorTitle}";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
