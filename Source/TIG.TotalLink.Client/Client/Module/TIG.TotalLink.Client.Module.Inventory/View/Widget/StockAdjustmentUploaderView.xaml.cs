using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Stock Adjustment Uploader", "Inventory", "Uploads Stock Adjustments that were imported from a spreadsheet.")]
    public partial class StockAdjustmentUploaderView
    {
        #region Constructors

        public StockAdjustmentUploaderView()
        {
            InitializeComponent();
        }

        public StockAdjustmentUploaderView(StockAdjustmentUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
