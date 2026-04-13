using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.SalesOrder;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.SalesOrder
{
    [Widget("Sales Order List", "Sales", "A list of Sales Orders.")]
    public partial class SalesOrderListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SalesOrderListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SalesOrderListView(SalesOrderListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
