using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Repository.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Repository.View.Widget
{
    [Widget("Sync Control", "Repository", "Control sync records")]
    [HideWidget(HostTypes.ServerManager)]
    public partial class SyncControlView
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SyncControlView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with data context
        /// </summary>
        /// <param name="viewModel">Sync control view model</param>
        public SyncControlView(SyncControlViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
