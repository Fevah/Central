using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Sku List", "Inventory", "A list of Skus.")]
    public partial class SkuListView
    {
        #region Constructors

        public SkuListView()
        {
            InitializeComponent();
        }

        public SkuListView(SkuListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
