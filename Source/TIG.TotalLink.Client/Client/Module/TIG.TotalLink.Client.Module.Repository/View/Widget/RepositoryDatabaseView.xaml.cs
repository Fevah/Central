using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Repository.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Repository.View.Widget
{
    [Widget("Repository Database", "Server", "Configures the repository database connection.")]
    [HideWidget(HostTypes.Client)]
    public partial class RepositoryDatabaseView
    {
        public RepositoryDatabaseView()
        {
            InitializeComponent();
        }

        public RepositoryDatabaseView(RepositoryDatabaseViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
