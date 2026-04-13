using TIG.TotalLink.Client.Module.Integration.ViewModel.Widget;
using TIG.TotalLink.Client.Module.Admin.Attribute;


namespace TIG.TotalLink.Client.Module.Integration.View.Widget
{
    [Widget("Integration Mapping List", "Integration", "A list of Integration Mappings.")]
    public partial class SyncEntityMapListView
    {
        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SyncEntityMapListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SyncEntityMapListView(SyncEntityMapListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
