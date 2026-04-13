using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Physical Stock List", "Inventory", "A list of Physical Stock.")]
    public partial class PhysicalStockListView
    {
        #region Constructors

        public PhysicalStockListView()
        {
            InitializeComponent();
        }

        public PhysicalStockListView(PhysicalStockListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
