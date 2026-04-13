using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Quote;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Quote
{
    [Widget("Quote Version List", "Sales", "A list of Quote Versions.")]
    public partial class QuoteVersionListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public QuoteVersionListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public QuoteVersionListView(QuoteVersionListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
