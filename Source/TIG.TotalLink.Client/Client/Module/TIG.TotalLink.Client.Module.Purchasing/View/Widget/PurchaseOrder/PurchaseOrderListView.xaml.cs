using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseOrder;

namespace TIG.TotalLink.Client.Module.Purchasing.View.Widget.PurchaseOrder
{
    [Widget("Purchase Order List", "Purchasing", "A list of Purchase Orders.")]
    public partial class PurchaseOrderListView
    {
        #region Constructors

        public PurchaseOrderListView()
        {
            InitializeComponent();
        }

        public PurchaseOrderListView(PurchaseOrderListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
