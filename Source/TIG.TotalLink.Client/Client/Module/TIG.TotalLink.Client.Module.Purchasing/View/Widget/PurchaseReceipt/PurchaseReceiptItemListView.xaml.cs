using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseReceipt;

namespace TIG.TotalLink.Client.Module.Purchasing.View.Widget.PurchaseReceipt
{
    [Widget("Purchase Receipt Item List", "Purchasing", "A list of Purchase Receipt Items.")]
    public partial class PurchaseReceiptItemListView
    {
        #region Constructors

        public PurchaseReceiptItemListView()
        {
            InitializeComponent();
        }

        public PurchaseReceiptItemListView(PurchaseReceiptItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
