using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Delivery;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Delivery
{
    [Widget("Pick Item List", "Sales", "A list of Pick Items.")]
    public partial class PickItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public PickItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public PickItemListView(PickItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
