using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseOrder;

namespace TIG.TotalLink.Client.Module.Purchasing.View.Widget.PurchaseOrder
{
    [Widget("Purchase Order Item List", "Purchasing", "A list of Purchase Order Items.")]
    public partial class PurchaseOrderItemListView
    {
        #region Constructors

        public PurchaseOrderItemListView()
        {
            InitializeComponent();
        }

        public PurchaseOrderItemListView(PurchaseOrderItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
