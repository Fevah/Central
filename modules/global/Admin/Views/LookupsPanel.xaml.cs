using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

public partial class LookupsPanel : System.Windows.Controls.UserControl
{
    public LookupsPanel()
    {
        InitializeComponent();
    }

    public TreeListControl Grid => AdminGrid;
    public TreeListView View => AdminView;

    // ── Events delegated to host ──
    public event Func<LookupItem, Task>? SaveLookup;
}
