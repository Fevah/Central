using System.Collections;

namespace Central.Desktop.Services;

/// <summary>
/// Engine-level drill-down window for any chart click.
/// Shows SD request data in a pre-defined grid layout.
/// </summary>
public partial class ChartDrillDownWindow : DevExpress.Xpf.Core.DXWindow
{
    public ChartDrillDownWindow(string title, IEnumerable data)
    {
        InitializeComponent();
        Title = title;
        HeaderLabel.Text = title;
        DrillGrid.ItemsSource = data;
    }
}
