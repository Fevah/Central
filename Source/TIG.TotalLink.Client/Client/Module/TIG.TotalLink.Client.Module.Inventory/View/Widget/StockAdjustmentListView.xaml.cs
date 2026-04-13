using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Stock Adjustment List", "Inventory", "A list of Stock Adjustments.")]
    public partial class StockAdjustmentListView
    {
        #region Constructors

        public StockAdjustmentListView()
        {
            InitializeComponent();
        }

        public StockAdjustmentListView(StockAdjustmentListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
