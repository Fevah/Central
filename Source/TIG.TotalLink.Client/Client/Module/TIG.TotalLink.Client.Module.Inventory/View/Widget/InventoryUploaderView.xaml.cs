using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Inventory Uploader", "Inventory", "Uploads Styles and Skus that were imported from a spreadsheet.")]
    public partial class InventoryUploaderView
    {
        #region Constructors

        public InventoryUploaderView()
        {
            InitializeComponent();
        }

        public InventoryUploaderView(InventoryUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
