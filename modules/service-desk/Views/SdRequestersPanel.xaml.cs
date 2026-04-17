using DevExpress.Xpf.Grid;
using Central.Core.Models;
using Central.Core.Shell;
using Central.Module.ServiceDesk.Services;

namespace Central.Module.ServiceDesk.Views;

public partial class SdRequestersPanel : System.Windows.Controls.UserControl
{
    public SdRequestersPanel()
    {
        InitializeComponent();
        ReqGrid.CurrentItemChanged += (_, _) =>
        {
            if (ReqGrid.CurrentItem is SdRequester r)
                PanelMessageBus.Publish(new LinkSelectionMessage("sdrequesters", "RequesterName", r.Name));
        };
    }
    public GridControl Grid => ReqGrid;
    public TableView View => ReqView;
}
