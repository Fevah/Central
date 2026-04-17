using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

/// <summary>
/// Scheduler panel — appointment grid with day/week/month navigation,
/// resource filtering, and full CRUD. Uses DX GridControl as the base.
/// When DX Scheduler license is available, swap to native SchedulerControl.
/// </summary>
public partial class SchedulerPanel : System.Windows.Controls.UserControl
{
    private DateTime _currentDate = DateTime.Today;
    private string _viewMode = "Week";

    public SchedulerPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => AppointmentsGrid;
    public TableView View => AppointmentsView;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<Appointment> Appointments { get; } = new();
    public ObservableCollection<AppointmentResource> Resources { get; } = new();

    // ── Delegates wired by shell ──
    public Func<Appointment, Task>? SaveAppointment { get; set; }
    public Func<int, Task>? DeleteAppointment { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<Appointment> appointments, IEnumerable<AppointmentResource> resources)
    {
        Appointments.Clear();
        foreach (var a in appointments) Appointments.Add(a);
        Resources.Clear();
        foreach (var r in resources) Resources.Add(r);

        AppointmentsGrid.ItemsSource = Appointments;

        // Populate resource combo
        var items = new List<object> { "(All Resources)" };
        items.AddRange(Resources.Select(r => (object)r.DisplayName));
        ResourceCombo.ItemsSource = items;

        UpdatePeriodLabel();
        ApplyDateFilter();
        StatusLabel.Text = $"{Appointments.Count} appointments, {Resources.Count} resources";
    }

    /// <summary>Get the visible date range for loading data.</summary>
    public (DateTime Start, DateTime End) GetVisibleRange()
    {
        return (_currentDate.AddMonths(-1), _currentDate.AddMonths(2));
    }

    private void UpdatePeriodLabel()
    {
        PeriodLabel.Text = _viewMode switch
        {
            "Day" => _currentDate.ToString("dddd, dd MMMM yyyy"),
            "Week" => $"{StartOfWeek(_currentDate):dd MMM} - {StartOfWeek(_currentDate).AddDays(6):dd MMM yyyy}",
            "Month" => _currentDate.ToString("MMMM yyyy"),
            _ => _currentDate.ToString("dd MMMM yyyy")
        };
    }

    private void ApplyDateFilter()
    {
        DateTime start, end;
        switch (_viewMode)
        {
            case "Day":
                start = _currentDate.Date;
                end = start.AddDays(1);
                break;
            case "Week":
                start = StartOfWeek(_currentDate);
                end = start.AddDays(7);
                break;
            default: // Month
                start = new DateTime(_currentDate.Year, _currentDate.Month, 1);
                end = start.AddMonths(1);
                break;
        }

        // Apply CriteriaOperator filter for the date range
        AppointmentsGrid.FilterCriteria = new DevExpress.Data.Filtering.BetweenOperator(
            "StartTime", start, end);
    }

    private static DateTime StartOfWeek(DateTime dt)
    {
        int diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.AddDays(-diff).Date;
    }

    // ── Navigation ────────────────────────────────────────────────────────

    private void PrevPeriod_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _viewMode switch
        {
            "Day" => _currentDate.AddDays(-1),
            "Week" => _currentDate.AddDays(-7),
            "Month" => _currentDate.AddMonths(-1),
            _ => _currentDate.AddDays(-7)
        };
        UpdatePeriodLabel();
        ApplyDateFilter();
    }

    private void NextPeriod_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = _viewMode switch
        {
            "Day" => _currentDate.AddDays(1),
            "Week" => _currentDate.AddDays(7),
            "Month" => _currentDate.AddMonths(1),
            _ => _currentDate.AddDays(7)
        };
        UpdatePeriodLabel();
        ApplyDateFilter();
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _currentDate = DateTime.Today;
        UpdatePeriodLabel();
        ApplyDateFilter();
    }

    private void ViewCombo_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        _viewMode = ViewCombo.EditValue?.ToString() ?? "Week";
        UpdatePeriodLabel();
        ApplyDateFilter();
    }

    private void ResourceCombo_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var sel = ResourceCombo.EditValue?.ToString();
        if (string.IsNullOrEmpty(sel) || sel == "(All Resources)")
        {
            // Clear resource filter — only keep date filter
            ApplyDateFilter();
        }
        else
        {
            var resource = Resources.FirstOrDefault(r => r.DisplayName == sel);
            if (resource != null)
            {
                // Combine date + resource filter
                var dateFilter = AppointmentsGrid.FilterCriteria;
                var resFilter = new DevExpress.Data.Filtering.BinaryOperator("ResourceId",
                    resource.Id, DevExpress.Data.Filtering.BinaryOperatorType.Equal);
                AppointmentsGrid.FilterCriteria = dateFilter != null
                    ? DevExpress.Data.Filtering.GroupOperator.And(dateFilter, resFilter)
                    : resFilter;
            }
        }
    }

    // ── CRUD ──────────────────────────────────────────────────────────────

    private async void NewAppointment_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTime.Now;
        var appt = new Appointment
        {
            Subject = "",
            StartTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0),
            EndTime = new DateTime(now.Year, now.Month, now.Day, now.Hour + 1, 0, 0),
            CreatedBy = Central.Engine.Auth.AuthContext.Instance.CurrentUser?.Id
        };

        var dialog = new AppointmentEditDialog(appt, Resources);
        if (dialog.ShowDialog() == true)
        {
            if (SaveAppointment != null) await SaveAppointment(dialog.Appointment);
            Appointments.Add(dialog.Appointment);
            AppointmentsGrid.SelectedItem = dialog.Appointment;
            StatusLabel.Text = $"Created: {dialog.Appointment.Subject}";
        }
    }

    private async void EditAppointment_Click(object sender, RoutedEventArgs e)
    {
        if (AppointmentsGrid.SelectedItem is not Appointment appt) return;

        var dialog = new AppointmentEditDialog(appt, Resources);
        if (dialog.ShowDialog() == true)
        {
            if (SaveAppointment != null) await SaveAppointment(dialog.Appointment);
            StatusLabel.Text = $"Saved: {dialog.Appointment.Subject}";
        }
    }

    private async void DeleteAppointment_Click(object sender, RoutedEventArgs e)
    {
        if (AppointmentsGrid.SelectedItem is not Appointment appt) return;
        if (System.Windows.MessageBox.Show($"Delete appointment '{appt.Subject}'?",
            "Confirm", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;

        if (appt.Id > 0 && DeleteAppointment != null) await DeleteAppointment(appt.Id);
        Appointments.Remove(appt);
        StatusLabel.Text = $"Deleted. {Appointments.Count} appointments remaining";
    }

    private void AppointmentsView_RowDoubleClick(object sender, RowDoubleClickEventArgs e)
    {
        EditAppointment_Click(sender, new RoutedEventArgs());
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }
}
