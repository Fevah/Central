using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Global.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Global.View.Widget
{
    [HideWidget(HostTypes.Client)]
    [Widget("Service List", "Server", "Allows management of all TotalLink services.")]
    public partial class ServiceListView : UserControl
    {
        public ServiceListView()
        {
            InitializeComponent();
        }

        public ServiceListView(ServiceListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
