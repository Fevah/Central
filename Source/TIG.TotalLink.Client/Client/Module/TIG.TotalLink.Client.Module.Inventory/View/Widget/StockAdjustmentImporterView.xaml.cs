using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Stock Adjustment Importer", "Inventory", "Imports Stock Adjustments from a spreadsheet..")]
    public partial class StockAdjustmentImporterView
    {
        #region Constructors

        public StockAdjustmentImporterView()
        {
            InitializeComponent();
        }

        public StockAdjustmentImporterView(StockAdjustmentImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
