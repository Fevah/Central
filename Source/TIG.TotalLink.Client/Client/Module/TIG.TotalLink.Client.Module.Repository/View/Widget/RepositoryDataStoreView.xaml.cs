using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Repository.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Repository.View.Widget
{
    [Widget("Repository DataStore", "Server", "List of repository datastores.")]
    [HideWidget(HostTypes.Client)]
    public partial class RepositoryDataStoreView
    {
        public RepositoryDataStoreView()
        {
            InitializeComponent();
        }

        public RepositoryDataStoreView(RepositoryDataStoreViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
