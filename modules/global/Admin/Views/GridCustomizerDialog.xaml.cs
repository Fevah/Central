using System.Windows;
using DevExpress.Xpf.Core;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

/// <summary>
/// Grid Customizer dialog — allows users to configure grid display settings
/// (row height, alternating rows, summary footer, group panel, auto-filter row).
/// </summary>
public partial class GridCustomizerDialog : DXWindow
{
    public GridCustomizerDialog()
    {
        InitializeComponent();
    }

    /// <summary>Get or set the grid settings shown in the dialog.</summary>
    public GridSettings GridSettings
    {
        get => new GridSettings
        {
            RowHeight = (int)(decimal)RowHeightSpin.Value,
            UseAlternatingRows = AlternatingRowsCheck.IsChecked == true,
            ShowSummaryFooter = SummaryFooterCheck.IsChecked == true,
            ShowGroupPanel = GroupPanelCheck.IsChecked == true,
            ShowAutoFilterRow = AutoFilterRowCheck.IsChecked == true
        };
        set
        {
            if (value == null) return;
            RowHeightSpin.Value = value.RowHeight;
            AlternatingRowsCheck.IsChecked = value.UseAlternatingRows;
            SummaryFooterCheck.IsChecked = value.ShowSummaryFooter;
            GroupPanelCheck.IsChecked = value.ShowGroupPanel;
            AutoFilterRowCheck.IsChecked = value.ShowAutoFilterRow;
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
