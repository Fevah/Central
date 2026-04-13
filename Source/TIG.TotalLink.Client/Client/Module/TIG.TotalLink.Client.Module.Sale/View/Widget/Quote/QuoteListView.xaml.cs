using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Quote
{
    [Widget("Quote List", "Sales", "A list of Quotes.")]
    public partial class QuoteListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public QuoteListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public QuoteListView(QuoteListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
