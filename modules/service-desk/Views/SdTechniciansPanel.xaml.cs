using DevExpress.Xpf.Grid;
using Central.Core.Models;
using Central.Core.Shell;
using Central.Module.ServiceDesk.Services;

namespace Central.Module.ServiceDesk.Views;

public partial class SdTechniciansPanel : System.Windows.Controls.UserControl
{
    public SdTechniciansPanel()
    {
        InitializeComponent();
        TechGrid.CurrentItemChanged += (_, _) =>
        {
            if (TechGrid.CurrentItem is SdTechnician tech)
                PanelMessageBus.Publish(new LinkSelectionMessage("sdtechnicians", "TechnicianName", tech.Name));
        };
    }

    public GridControl Grid => TechGrid;
    public TableView View => TechView;

    public Func<long, bool, Task>? SaveActive { get; set; }

    private async void TechView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Column.FieldName == "IsActive" && e.Row is SdTechnician tech && SaveActive != null)
            await SaveActive(tech.Id, tech.IsActive);
    }
}
