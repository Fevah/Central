using System;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class TimesheetPanel : System.Windows.Controls.UserControl
{
    public TimesheetPanel()
    {
        InitializeComponent();
        WeekPicker.EditValue = DateTime.Today;
    }

    public DevExpress.Xpf.Grid.GridControl Grid => TimesheetGrid;

    public event Func<TimeEntry, Task>? SaveEntry;
    public event Func<DateTime, DateTime, Task>? WeekChanged; // from, to
    public event Action? AddEntryRequested;

    public void UpdateTotal(decimal totalHours) => TotalHoursText.Text = $"Total: {totalHours:n2}h";

    private void WeekPicker_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        if (WeekPicker.EditValue is DateTime dt)
        {
            var monday = dt.AddDays(-(int)dt.DayOfWeek + (int)DayOfWeek.Monday);
            var sunday = monday.AddDays(6);
            WeekChanged?.Invoke(monday, sunday);
        }
    }

    private void AddEntry_Click(object sender, System.Windows.RoutedEventArgs e) => AddEntryRequested?.Invoke();

    private async void TimesheetView_ValidateRow(object sender, DevExpress.Xpf.Grid.GridRowValidationEventArgs e)
    {
        if (e.Row is TimeEntry entry && SaveEntry != null)
        {
            try { await SaveEntry(entry); }
            catch (Exception ex) { e.ErrorContent = ex.Message; e.IsValid = false; }
        }
    }

    private void TimesheetView_InvalidRowException(object sender, DevExpress.Xpf.Grid.InvalidRowExceptionEventArgs e)
        => e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;
}
