using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Invoice;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Invoice
{
    [Widget("Invoice Item List", "Sales", "A list of Invoice Items.")]
    public partial class InvoiceItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public InvoiceItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public InvoiceItemListView(InvoiceItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
