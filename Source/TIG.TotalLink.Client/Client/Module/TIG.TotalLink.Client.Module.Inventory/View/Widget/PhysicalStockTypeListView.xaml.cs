using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Physical Stock Type List", "Inventory", "A list of Physical Stock Types.")]
    public partial class PhysicalStockTypeListView
    {
        #region Constructors

        public PhysicalStockTypeListView()
        {
            InitializeComponent();
        }

        public PhysicalStockTypeListView(PhysicalStockTypeListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
