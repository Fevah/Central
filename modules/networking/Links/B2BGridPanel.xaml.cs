using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Networking.Links;

public partial class B2BGridPanel : System.Windows.Controls.UserControl
{
    public B2BGridPanel()
    {
        InitializeComponent();
        B2BGrid.MasterRowExpanded += (_, e) =>
        {
            if (B2BGrid.GetRow(e.RowHandle) is B2BLink link && link.DetailConfigLines.Count == 0)
                link.GenerateDetailConfig();
        };
    }

    public GridControl Grid => B2BGrid;
    public TableView View => B2BView;

    public void BindComboSources(object buildings, object statuses)
    {
        B2BBuildingACombo.ItemsSource = buildings;
        B2BBuildingBCombo.ItemsSource = buildings;
        B2BStatusCombo.ItemsSource = statuses;
    }

    // ── Events delegated to host ──
    public event Func<B2BLink, Task>? SaveLink;
    public event Action<object, CellValueChangedEventArgs>? CellChanged;
    public event Action<string, object>? ConfigCogClicked;  // side ("A"/"B"), row

    private async void B2BView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not B2BLink link) return;
        if (SaveLink != null)
            await SaveLink.Invoke(link);
    }

    private void B2BView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        => CellChanged?.Invoke(sender, e);

    private void ConfigCog_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string side)
        {
            var row = (btn.DataContext as DevExpress.Xpf.Grid.EditGridCellData)?.RowData?.Row;
            ConfigCogClicked?.Invoke(side, row!);
        }
    }

    // ─── Engine context + audit-drill context menu ─────────────────────

    private string? _engineBaseUrl;
    private Guid _engineTenantId;
    private int? _engineActorUserId;

    public void SetEngineContext(string baseUrl, Guid tenantId, int? actorUserId = null)
    {
        _engineBaseUrl = baseUrl;
        _engineTenantId = tenantId;
        _engineActorUserId = actorUserId;
    }

    private async void OnContextShowAudit(object sender, System.Windows.RoutedEventArgs e)
    {
        if (B2BGrid.CurrentItem is B2BLink link)
            await LinkAuditDrill.ShowAuditForLinkAsync(
                _engineBaseUrl, _engineTenantId, _engineActorUserId, link);
    }

    private void OnContextCopyLinkCode(object sender, System.Windows.RoutedEventArgs e)
        => LinkAuditDrill.CopyLinkCode(B2BGrid.CurrentItem as B2BLink);

    private void OnContextSearchFromHere(object sender, System.Windows.RoutedEventArgs e)
        => LinkAuditDrill.SearchFromHere(B2BGrid.CurrentItem as B2BLink);
}
