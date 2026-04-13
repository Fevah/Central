using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Quote
{
    /// <summary>
    /// Quote list view.
    /// </summary>
    [Widget(Name = "Quote Tree List", Category = "Sale", Description = "A tree list of quotes.")]
    public partial class QuoteTreeListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public QuoteTreeListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public QuoteTreeListView(QuoteTreeListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
