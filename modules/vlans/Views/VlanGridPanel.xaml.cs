using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.VLANs.Views;

public partial class VlanGridPanel : System.Windows.Controls.UserControl
{
    public VlanGridPanel()
    {
        InitializeComponent();
        VlansView.ValidateRow += VlansView_ValidateRow;
        VlansView.InvalidRowException += (_, e) => e.ExceptionMode = ExceptionMode.NoAction;
        VlansGrid.MasterRowExpanded += VlansGrid_MasterRowExpanded;
    }

    /// <summary>Fired when VLAN row expanded — host loads sites with this VLAN.</summary>
    public event System.Func<VlanEntry, System.Threading.Tasks.Task>? LoadDetailSites;

    private async void VlansGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (VlansGrid.GetRow(e.RowHandle) is VlanEntry vlan && vlan.DetailSites.Count == 0)
        {
            if (LoadDetailSites != null)
                await LoadDetailSites(vlan);
        }
    }

    private async void VlansView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is VlanEntry vlan && SaveVlan != null)
            await SaveVlan.Invoke(vlan);
    }

    public GridControl Grid => VlansGrid;
    public TableView View => VlansView;

    public void BindComboSources(object? subnets, object? statuses)
    {
        if (subnets != null) VlansSubnetCombo.ItemsSource = subnets;
        if (statuses != null) VlansStatusCombo.ItemsSource = statuses;
    }

    // ── Events delegated to host ──
    public event Func<VlanEntry, Task>? SaveVlan;
    public event Action? BlockLockedChanged;

    /// <summary>All VLAN entries for cross-row updates. Set by the host.</summary>
    public System.Collections.ObjectModel.ObservableCollection<VlanEntry>? AllVlanEntries { get; set; }

    private readonly Dictionary<int, List<(string Field, string OldVal, string NewVal)>> _pendingChanges = new();

    private void VlansView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Column?.FieldName == "Subnet" && e.Row is VlanEntry v)
        {
            v.BlockLocked = v.Subnet == "/21";
            BlockLockedChanged?.Invoke();
        }

        if (e.Row is VlanEntry vlan && e.Column != null)
        {
            if (!_pendingChanges.ContainsKey(vlan.Id))
                _pendingChanges[vlan.Id] = new();
            var oldStr = e.OldValue?.ToString() ?? "";
            var newStr = e.Value?.ToString() ?? "";
            if (oldStr != newStr)
                _pendingChanges[vlan.Id].Add((e.Column.FieldName, oldStr, newStr));
        }
    }

    private void VlansView_ShownEditor(object sender, EditorEventArgs e)
    {
        if (e.Column?.FieldName != "Subnet") return;
        if (View.ActiveEditor is DevExpress.Xpf.Editors.ComboBoxEdit combo)
        {
            var row = Grid.GetRow(View.FocusedRowHandle) as VlanEntry;
            combo.ItemsSource = (row != null && row.IsBlockRoot) ? new[] { "/21", "/24" } : new[] { "/24" };
            combo.EditValueChanged -= SubnetEditor_Changed;
            combo.EditValueChanged += SubnetEditor_Changed;
        }
    }

    private void SubnetEditor_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        View.PostEditor();
        var row = Grid.GetRow(View.FocusedRowHandle) as VlanEntry;
        if (row == null || AllVlanEntries == null) return;

        row.BlockLocked = row.Subnet == "/21";

        if (int.TryParse(row.VlanId, out var vlanNum) && vlanNum <= 255)
        {
            int blockStart = (vlanNum / 8) * 8;

            if (row.Subnet == "/21")
            {
                row.Gateway = $"10.x.{blockStart + 7}.254";
                row.UsableRange = $"10.x.{blockStart}.1 - 10.x.{blockStart + 7}.254";
                foreach (var v in AllVlanEntries)
                {
                    if (v == row) continue;
                    if (int.TryParse(v.VlanId, out var n) && n >= blockStart && n < blockStart + 8)
                    { v.Gateway = ""; v.UsableRange = ""; }
                }
            }
            else
            {
                row.Gateway = $"10.x.{vlanNum}.254";
                row.UsableRange = $"10.x.{vlanNum}.1-254";
                foreach (var v in AllVlanEntries)
                {
                    if (v == row) continue;
                    if (int.TryParse(v.VlanId, out var n) && n >= blockStart && n < blockStart + 8)
                    { v.Gateway = $"10.x.{n}.254"; v.UsableRange = $"10.x.{n}.1-254"; }
                }
            }
        }
        BlockLockedChanged?.Invoke();
    }
}
