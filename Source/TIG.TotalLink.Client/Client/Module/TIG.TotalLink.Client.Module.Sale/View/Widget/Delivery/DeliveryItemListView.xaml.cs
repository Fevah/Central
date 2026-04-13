using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Delivery;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Delivery
{
    [Widget("Delivery Item List", "Sales", "A list of Delivery Items.")]
    public partial class DeliveryItemListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DeliveryItemListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public DeliveryItemListView(DeliveryItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
