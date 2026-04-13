using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Integration.ViewModel.Widget;


namespace TIG.TotalLink.Client.Module.Integration.View.Widget
{
    [Widget("Integration Entity List", "Integration", "A list of Integration Entities.")]
    public partial class SyncEntityListView
    {
        #region Constructor

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SyncEntityListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SyncEntityListView(SyncEntityListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
