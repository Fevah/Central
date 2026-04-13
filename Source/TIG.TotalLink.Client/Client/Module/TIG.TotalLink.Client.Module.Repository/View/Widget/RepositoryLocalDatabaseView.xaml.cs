using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Repository.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Repository.View.Widget
{
    [Widget("Repository CacheStore", "Repository", "Configures the repository local database connection.")]
    [HideWidget(HostTypes.ServerManager)]
    public partial class RepositoryLocalDatabaseView
    {
        public RepositoryLocalDatabaseView()
        {
            InitializeComponent();
        }

        public RepositoryLocalDatabaseView(RepositoryLocalDatabaseViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
