using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Warehouse Location List", "Inventory", "A list of Warehouse Locations.")]
    public partial class WarehouseLocationListView
    {
        #region Constructors

        public WarehouseLocationListView()
        {
            InitializeComponent();
        }

        public WarehouseLocationListView(WarehouseLocationListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
