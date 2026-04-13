using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Global.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Global.View.Widget
{
    [HideWidget(HostTypes.Client)]
    [Widget("Reference Configuration", "Server", "Configures global settings for generating reference numbers.")]
    public partial class ReferenceConfigView : UserControl
    {
        public ReferenceConfigView()
        {
            InitializeComponent();
        }

        public ReferenceConfigView(ReferenceConfigViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
