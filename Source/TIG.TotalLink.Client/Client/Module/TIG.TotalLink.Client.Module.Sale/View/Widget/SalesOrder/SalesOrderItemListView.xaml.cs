using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.SalesOrder;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.SalesOrder
{
    [Widget("Sales Order Item List", "Sales", "A list of Sales Order Items.")]
    public partial class SalesOrderItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SalesOrderItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SalesOrderItemListView(SalesOrderItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
