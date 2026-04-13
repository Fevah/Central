using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Bin Location List", "Inventory", "A list of Bin Locations.")]
    public partial class BinLocationListView
    {
        #region Constructors

        public BinLocationListView()
        {
            InitializeComponent();
        }

        public BinLocationListView(BinLocationListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
