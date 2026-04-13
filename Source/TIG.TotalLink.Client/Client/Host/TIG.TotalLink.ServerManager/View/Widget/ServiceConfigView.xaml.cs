using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.ServerManager.ViewModel.Widget;

namespace TIG.TotalLink.ServerManager.View.Widget
{
    [Widget("Service Configuration", "Service", "Configures the primary settings for connecting to services.")]
    public partial class ServiceConfigView : UserControl
    {
        public ServiceConfigView()
        {
            InitializeComponent();
        }

        public ServiceConfigView(ServiceConfigViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
