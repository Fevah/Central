using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Integration.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Integration.View.Widget
{
    [Widget("Integration Bundles List", "Integration", "A list of Integration Bundles.")]
    public partial class SyncEntityBundleListView
    {
        #region Constructors

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public SyncEntityBundleListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SyncEntityBundleListView(SyncEntityBundleListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
