using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class Appointment : INotifyPropertyChanged
{
    private int _id;
    private string _subject = "";
    private string _description = "";
    private DateTime _startTime;
    private DateTime _endTime;
    private bool _allDay;
    private string _location = "";
    private int? _resourceId;
    private int _status;
    private int _label;
    private string _recurrenceInfo = "";
    private int? _taskId;
    private long? _ticketId;
    private int? _createdBy;
    private DateTime _createdAt;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Subject { get => _subject; set { _subject = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public DateTime StartTime { get => _startTime; set { _startTime = value; OnPropertyChanged(); } }
    public DateTime EndTime { get => _endTime; set { _endTime = value; OnPropertyChanged(); } }
    public bool AllDay { get => _allDay; set { _allDay = value; OnPropertyChanged(); } }
    public string Location { get => _location; set { _location = value; OnPropertyChanged(); } }
    public int? ResourceId { get => _resourceId; set { _resourceId = value; OnPropertyChanged(); } }
    public int Status { get => _status; set { _status = value; OnPropertyChanged(); } }
    public int Label { get => _label; set { _label = value; OnPropertyChanged(); } }
    public string RecurrenceInfo { get => _recurrenceInfo; set { _recurrenceInfo = value; OnPropertyChanged(); } }
    public int? TaskId { get => _taskId; set { _taskId = value; OnPropertyChanged(); } }
    public long? TicketId { get => _ticketId; set { _ticketId = value; OnPropertyChanged(); } }
    public int? CreatedBy { get => _createdBy; set { _createdBy = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class AppointmentResource : INotifyPropertyChanged
{
    private int _id;
    private int _userId;
    private string _displayName = "";
    private string _color = "#3B82F6";
    private bool _isActive = true;

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public int UserId { get => _userId; set { _userId = value; OnPropertyChanged(); } }
    public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }
    public string Color { get => _color; set { _color = value; OnPropertyChanged(); } }
    public bool IsActive { get => _isActive; set { _isActive = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
