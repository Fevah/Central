using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Repository.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Repository.View.Widget
{
    [Widget("Repository File List", "Repository", "A list of Repository File List.")]
    [HideWidget(HostTypes.ServerManager)]
    public partial class RepositoryFileListView
    {
        #region Constructor

        /// <summary>
        /// Default Constructor
        /// </summary>
        public RepositoryFileListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to service UI part.</param>
        public RepositoryFileListView(RepositoryFileListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
