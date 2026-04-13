using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Invoice;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Invoice
{
    [Widget("Invoice List", "Sales", "A list of Invoices.")]
    public partial class InvoiceListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public InvoiceListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public InvoiceListView(InvoiceListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
