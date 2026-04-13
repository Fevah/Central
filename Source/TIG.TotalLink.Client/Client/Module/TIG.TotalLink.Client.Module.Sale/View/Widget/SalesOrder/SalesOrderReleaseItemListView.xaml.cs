using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.SalesOrder;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.SalesOrder
{
    [Widget("Sales Order Release Item List", "Sales", "A list of Sales Order Release Items.")]
    public partial class SalesOrderReleaseItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public SalesOrderReleaseItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public SalesOrderReleaseItemListView(SalesOrderReleaseItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
