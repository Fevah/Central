using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Quote
{
    [Widget("Quote Item List", "Sales", "A list of Quote Items.")]
    public partial class QuoteItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public QuoteItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public QuoteItemListView(QuoteItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
