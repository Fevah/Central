using Central.Engine.Models;

namespace Central.Module.Global.Admin;

/// <summary>
/// Edit dialog for appointments. Used by SchedulerPanel on New/Edit/DoubleClick.
/// </summary>
public partial class AppointmentEditDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly Appointment _appointment;

    public AppointmentEditDialog(Appointment appointment, IEnumerable<AppointmentResource>? resources = null)
    {
        _appointment = appointment;
        InitializeComponent();

        // Populate fields
        SubjectEdit.Text = appointment.Subject;
        StartEdit.DateTime = appointment.StartTime == default ? DateTime.Now : appointment.StartTime;
        EndEdit.DateTime = appointment.EndTime == default ? DateTime.Now.AddHours(1) : appointment.EndTime;
        AllDayCheck.IsChecked = appointment.AllDay;
        LocationEdit.Text = appointment.Location;
        DescriptionEdit.Text = appointment.Description;
        StatusCombo.EditValue = appointment.Status.ToString();
        TaskIdEdit.Value = appointment.TaskId ?? 0;

        // Populate resource dropdown
        if (resources != null)
        {
            var items = resources.Select(r => new { r.Id, r.DisplayName }).ToList();
            ResourceCombo.ItemsSource = items;
            ResourceCombo.DisplayMember = "DisplayName";
            ResourceCombo.ValueMember = "Id";
            if (appointment.ResourceId.HasValue)
                ResourceCombo.EditValue = appointment.ResourceId.Value;
        }

        Title = appointment.Id > 0 ? $"Edit Appointment — {appointment.Subject}" : "New Appointment";
    }

    /// <summary>The edited appointment (updated on Save).</summary>
    public Appointment Appointment => _appointment;

    private void Save_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SubjectEdit.Text))
        {
            System.Windows.MessageBox.Show("Subject is required.", "Validation");
            return;
        }

        _appointment.Subject = SubjectEdit.Text.Trim();
        _appointment.StartTime = StartEdit.DateTime;
        _appointment.EndTime = EndEdit.DateTime;
        _appointment.AllDay = AllDayCheck.IsChecked == true;
        _appointment.Location = LocationEdit.Text?.Trim() ?? "";
        _appointment.Description = DescriptionEdit.Text?.Trim() ?? "";
        _appointment.Status = int.TryParse(StatusCombo.EditValue?.ToString(), out var st) ? st : 0;
        _appointment.TaskId = (int?)TaskIdEdit.Value is > 0 ? (int)TaskIdEdit.Value : null;

        if (ResourceCombo.EditValue is int resId && resId > 0)
            _appointment.ResourceId = resId;
        else
            _appointment.ResourceId = null;

        DialogResult = true;
    }

    private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e) => DialogResult = false;
}
