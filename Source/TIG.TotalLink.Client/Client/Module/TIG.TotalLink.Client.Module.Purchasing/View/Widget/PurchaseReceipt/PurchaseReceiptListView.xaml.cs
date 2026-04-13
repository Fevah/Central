using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Purchasing.ViewModel.Widget.PurchaseReceipt;

namespace TIG.TotalLink.Client.Module.Purchasing.View.Widget.PurchaseReceipt
{
    [Widget("Purchase Receipt List", "Purchasing", "A list of Purchase Receipts.")]
    public partial class PurchaseReceiptListView
    {
        #region Constructors

        public PurchaseReceiptListView()
        {
            InitializeComponent();
        }

        public PurchaseReceiptListView(PurchaseReceiptListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
