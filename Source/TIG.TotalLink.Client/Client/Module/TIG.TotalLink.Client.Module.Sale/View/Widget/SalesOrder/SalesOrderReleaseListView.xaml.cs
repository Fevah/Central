using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.SalesOrder;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.SalesOrder
{
    [Widget("Sales Order Release List", "Sales", "A list of Sales Order Releases.")]
    public partial class SalesOrderReleaseListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SalesOrderReleaseListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SalesOrderReleaseListView(SalesOrderReleaseListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
