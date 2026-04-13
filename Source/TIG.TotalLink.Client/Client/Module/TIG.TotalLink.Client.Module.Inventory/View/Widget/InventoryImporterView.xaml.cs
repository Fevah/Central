using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;


namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Inventory Importer", "Inventory", "Imports Styles and Skus from a spreadsheet.")]
    public partial class InventoryImporterView
    {
        #region Constructors

        public InventoryImporterView()
        {
            InitializeComponent();
        }

        public InventoryImporterView(InventoryImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
