using DevExpress.Xpf.Grid;
using Central.Engine.Models;
using Central.Engine.Shell;
using Central.Module.ServiceDesk.Services;

namespace Central.Module.ServiceDesk.Views;

public partial class SdGroupsPanel : System.Windows.Controls.UserControl
{
    public SdGroupsPanel()
    {
        InitializeComponent();
        GroupsGrid.CurrentItemChanged += (_, _) =>
        {
            if (GroupsGrid.CurrentItem is SdGroup g)
                PanelMessageBus.Publish(new LinkSelectionMessage("sdgroups", "GroupName", g.Name));
        };
    }

    public GridControl Grid => GroupsGrid;
    public TableView View => GroupsView;

    public Func<SdGroup, Task>? SaveGroup { get; set; }

    private async void GroupsView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is SdGroup g && SaveGroup != null)
            await SaveGroup(g);
    }
}
