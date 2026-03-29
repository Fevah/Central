using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class SdRequest : INotifyPropertyChanged
{
    private long _id;
    private string _displayId = "", _subject = "", _status = "", _priority = "";
    private string _groupName = "", _technicianName = "", _requesterName = "", _requesterEmail = "";
    private string _category = "", _site = "", _department = "", _template = "";
    private string _ticketUrl = "";
    private DateTime? _createdAt, _dueBy, _resolvedAt;
    private bool _isServiceRequest;

    public long Id { get => _id; set { _id = value; N(); } }
    public string DisplayId { get => _displayId; set { _displayId = value; N(); } }
    public string Subject { get => _subject; set { _subject = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); N(nameof(StatusColor)); MarkDirty(); } }
    public string Priority { get => _priority; set { _priority = value; N(); N(nameof(PriorityColor)); MarkDirty(); } }
    public string GroupName { get => _groupName; set { _groupName = value; N(); MarkDirty(); } }
    public string TechnicianName { get => _technicianName; set { _technicianName = value; N(); MarkDirty(); } }
    public string RequesterName { get => _requesterName; set { _requesterName = value; N(); } }
    public string RequesterEmail { get => _requesterEmail; set { _requesterEmail = value; N(); } }
    public string Category { get => _category; set { _category = value; N(); MarkDirty(); } }
    public string Site { get => _site; set { _site = value; N(); } }
    public string Department { get => _department; set { _department = value; N(); } }
    public string Template { get => _template; set { _template = value; N(); } }
    public string TicketUrl { get => _ticketUrl; set { _ticketUrl = value; N(); } }
    public DateTime? CreatedAt { get => _createdAt; set { _createdAt = value; N(); } }
    public DateTime? DueBy { get => _dueBy; set { _dueBy = value; N(); N(nameof(IsOverdue)); } }
    public DateTime? ResolvedAt { get => _resolvedAt; set { _resolvedAt = value; N(); } }
    public bool IsServiceRequest { get => _isServiceRequest; set { _isServiceRequest = value; N(); } }

    public long? TechnicianId { get; set; }
    public long? RequesterId { get; set; }
    public string? Description { get; set; }
    public string? Resolution { get; set; }

    // ── Dirty tracking (local changes not yet written back to ME) ──
    private bool _isDirty;
    private bool _trackChanges; // only set after initial load
    public bool IsDirty { get => _isDirty; set { _isDirty = value; N(); N(nameof(RowColor)); } }
    /// <summary>Amber background when dirty, transparent when clean.</summary>
    public string RowColor => _isDirty ? "#33F59E0B" : "#00000000";

    /// <summary>Snapshot of original values for building the ME write-back payload.</summary>
    public string OriginalStatus { get; private set; } = "";
    public string OriginalPriority { get; private set; } = "";
    public string OriginalGroupName { get; private set; } = "";
    public string OriginalTechnicianName { get; private set; } = "";
    public string OriginalCategory { get; private set; } = "";

    /// <summary>Call after loading from DB to start tracking changes.</summary>
    public void AcceptChanges()
    {
        OriginalStatus = _status;
        OriginalPriority = _priority;
        OriginalGroupName = _groupName;
        OriginalTechnicianName = _technicianName;
        OriginalCategory = _category;
        _isDirty = false;
        _trackChanges = true;
        N(nameof(IsDirty));
        N(nameof(RowColor));
    }

    private void MarkDirty()
    {
        if (!_trackChanges) return;
        if (_status != OriginalStatus || _priority != OriginalPriority ||
            _groupName != OriginalGroupName || _technicianName != OriginalTechnicianName ||
            _category != OriginalCategory)
        {
            if (!_isDirty) { _isDirty = true; N(nameof(IsDirty)); N(nameof(RowColor)); }
        }
        else
        {
            if (_isDirty) { _isDirty = false; N(nameof(IsDirty)); N(nameof(RowColor)); }
        }
    }

    /// <summary>Whether this ticket is considered done (Resolved or Closed both count).</summary>
    public bool IsClosed => Status == "Resolved" || Status == "Closed";

    public string StatusColor => Status switch
    {
        "Open" => "#3B82F6",
        "In Progress" => "#F59E0B",
        "On Hold" => "#8B5CF6",
        "Awaiting Response" => "#A855F7",
        "Resolved" or "Closed" => "#22C55E",
        "Canceled" or "Cancelled" => "#EF4444",
        "Archive" => "#4B5563",
        _ => "#9CA3AF"
    };

    public string PriorityColor => Priority switch
    {
        "High" or "Urgent" => "#EF4444",
        "Medium" or "Normal" => "#F59E0B",
        "Low" => "#22C55E",
        _ => "#9CA3AF"
    };

    public bool IsOverdue => DueBy.HasValue && DueBy.Value < DateTime.UtcNow
        && !IsClosed && Status != "Canceled" && Status != "Cancelled" && Status != "Archive";
    public string OverdueIcon => IsOverdue ? "⚠" : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Parent category that maps to multiple ME groups. Used for dashboard filtering.</summary>
public class SdGroupCategory : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private bool _isActive = true;
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public List<string> Members { get; set; } = new();
    public int MemberCount => Members.Count;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>SD support group lookup.</summary>
public class SdGroup : INotifyPropertyChanged
{
    private int _id;
    private string _name = "";
    private bool _isActive = true;
    private int _sortOrder;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Name { get => _name; set { _name = value; N(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class SdRequester : INotifyPropertyChanged
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Department { get; set; } = "";
    public string Site { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public bool IsVip { get; set; }
    public int OpenTickets { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class SdTechnician : INotifyPropertyChanged
{
    private bool _isActive = true;

    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Department { get; set; } = "";
    public bool IsActive { get => _isActive; set { _isActive = value; N(); } }
    public int OpenTickets { get; set; }
    public int ResolvedTickets { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

// ── KPI card model ───────────────────────────────────────────────────

/// <summary>Dashboard KPI summary stats for a period.</summary>
public class SdKpiSummary
{
    public int Incoming { get; set; }
    public int Resolutions { get; set; }
    public int Escalations { get; set; }
    public int SlaCompliant { get; set; }
    public double AvgResolutionHours { get; set; }
    public int OpenCount { get; set; }
    public int ActiveTechCount { get; set; }
    // Previous period for trend %
    public int PrevIncoming { get; set; }
    public int PrevResolutions { get; set; }
    public int PrevEscalations { get; set; }
    public int PrevSlaCompliant { get; set; }
    public double PrevAvgResolutionHours { get; set; }
    public int PrevOpenCount { get; set; }
}

/// <summary>Team grouping for technicians.</summary>
public class SdTeam
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public List<string> Members { get; set; } = new();
}

// ── Chart data models ────────────────────────────────────────────────

/// <summary>Overview total: created vs completed per bucket, plus avg resolution days and open count.</summary>
public class SdWeeklyTotal
{
    public DateTime Day { get; set; }
    /// <summary>Flexible label set by the dashboard based on range.</summary>
    public string Label { get; set; } = "";
    public string DayLabel => Day.ToString("ddd");
    public int Created { get; set; }
    public int Completed { get; set; }
    /// <summary>Average days to resolve tickets closed in this bucket.</summary>
    public double AvgResolutionDays { get; set; }
    /// <summary>Number of open tickets at the end of this bucket.</summary>
    public int OpenCount { get; set; }
    /// <summary>Running cumulative total of created tickets up to this bucket.</summary>
    public int CumulativeCreated { get; set; }
    /// <summary>Running cumulative total of closed tickets up to this bucket.</summary>
    public int CumulativeClosed { get; set; }
}

/// <summary>Per-technician daily closures.</summary>
public class SdTechDaily
{
    public string TechnicianName { get; set; } = "";
    public DateTime Day { get; set; }
    public string DayLabel => Day.ToString("ddd");
    public int Closed { get; set; }
}

/// <summary>Ticket aging buckets per technician.</summary>
public class SdAgingBucket
{
    public string TechnicianName { get; set; } = "";
    public int Days0to1 { get; set; }
    public int Days1to2 { get; set; }
    public int Days2to4 { get; set; }
    public int Days4to7 { get; set; }
    public int Days7Plus { get; set; }
    public int Total => Days0to1 + Days1to2 + Days2to4 + Days4to7 + Days7Plus;
}
