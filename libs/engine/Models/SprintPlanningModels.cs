using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

/// <summary>Per-user capacity allocation within a sprint.</summary>
public class SprintAllocation : INotifyPropertyChanged
{
    private int _id;
    private int _sprintId;
    private int _userId;
    private string _userName = "";
    private decimal? _capacityHours;
    private decimal? _capacityPoints;

    public int Id { get => _id; set { _id = value; N(); } }
    public int SprintId { get => _sprintId; set { _sprintId = value; N(); } }
    public int UserId { get => _userId; set { _userId = value; N(); } }
    public string UserName { get => _userName; set { _userName = value; N(); } }
    public decimal? CapacityHours { get => _capacityHours; set { _capacityHours = value; N(); } }
    public decimal? CapacityPoints { get => _capacityPoints; set { _capacityPoints = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Daily burndown snapshot for a sprint.</summary>
public class SprintBurndownPoint
{
    public int Id { get; set; }
    public int SprintId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal PointsRemaining { get; set; }
    public decimal HoursRemaining { get; set; }
    public decimal PointsCompleted { get; set; }
    public decimal HoursCompleted { get; set; }

    /// <summary>Ideal points remaining (for burndown ideal line).</summary>
    public decimal? IdealPoints { get; set; }
    public decimal? IdealHours { get; set; }
}
