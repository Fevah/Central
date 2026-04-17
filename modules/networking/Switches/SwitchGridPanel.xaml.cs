using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Networking.Switches;

public partial class SwitchGridPanel : System.Windows.Controls.UserControl
{
    public SwitchGridPanel()
    {
        InitializeComponent();
        SwitchGrid.MasterRowExpanded += SwitchView_MasterRowExpanded;
    }

    public GridControl Grid => SwitchGrid;
    public TableView View => SwitchView;

    // ── Events delegated to host ──
    public event Func<SwitchRecord, Task>? SaveSwitch;
    public event Action<string>? SearchChanged;
    /// <summary>Fired when a master row is expanded — host loads interfaces for the switch.</summary>
    public event Func<SwitchRecord, Task>? LoadDetailInterfaces;

    private void SearchBox_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => SearchChanged?.Invoke(SearchBox.Text ?? string.Empty);

    private async void SwitchView_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (SwitchGrid.GetRow(e.RowHandle) is SwitchRecord sw && sw.DetailInterfaces.Count == 0)
        {
            if (LoadDetailInterfaces != null)
                await LoadDetailInterfaces(sw);
        }
    }
}
