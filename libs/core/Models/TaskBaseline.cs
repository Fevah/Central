namespace Central.Core.Models;

/// <summary>Baseline schedule snapshot for Gantt comparison.</summary>
public class TaskBaseline
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string BaselineName { get; set; } = "";
    public DateTime? StartDate { get; set; }
    public DateTime? FinishDate { get; set; }
    public decimal? Points { get; set; }
    public decimal? Hours { get; set; }
    public DateTime SavedAt { get; set; }
}

/// <summary>Gantt dependency link view model for DX GanttControl binding.</summary>
public class GanttPredecessorLink
{
    public int PredecessorTaskId { get; set; }
    public int SuccessorTaskId { get; set; }
    /// <summary>0=FinishToStart, 1=FinishToFinish, 2=StartToStart, 3=StartToFinish</summary>
    public int LinkType { get; set; }
    public int Lag { get; set; }
}
