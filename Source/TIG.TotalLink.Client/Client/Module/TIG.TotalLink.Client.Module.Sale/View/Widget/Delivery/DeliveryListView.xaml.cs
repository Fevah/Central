using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Sale.ViewModel.Widget.Delivery;

namespace TIG.TotalLink.Client.Module.Sale.View.Widget.Delivery
{
    [Widget("Delivery List", "Sales", "A list of Deliveries.")]
    public partial class DeliveryListView
    {
        #region Constructor

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DeliveryListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Constructor with view model.
        /// </summary>
        /// <param name="viewModel">ViewModel to support UI part.</param>
        public DeliveryListView(DeliveryListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
