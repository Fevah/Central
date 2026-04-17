using System.Collections.ObjectModel;
using DevExpress.Xpf.Grid;
using Central.Engine.Services;

namespace Central.Module.Admin.Views;

public partial class AuditLogPanel : System.Windows.Controls.UserControl
{
    public AuditLogPanel() => InitializeComponent();

    public GridControl Grid => AuditGrid;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<AuditEntry> Entries { get; } = new();

    public Func<int, string?, string?, Task<List<AuditEntry>>>? LoadAudit { get; set; }

    public void Load(IEnumerable<AuditEntry> entries)
    {
        Entries.Clear();
        foreach (var e in entries) Entries.Add(e);
        AuditGrid.ItemsSource = Entries;
        StatusLabel.Text = $"{Entries.Count} audit entries";
    }

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async void Filter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        if (LoadAudit == null) return;
        var entityType = EntityFilter.EditValue?.ToString();
        var username = string.IsNullOrWhiteSpace(UserFilter.Text) ? null : UserFilter.Text.Trim();
        var entries = await LoadAudit(500, entityType, username);
        Load(entries);
    }
}
