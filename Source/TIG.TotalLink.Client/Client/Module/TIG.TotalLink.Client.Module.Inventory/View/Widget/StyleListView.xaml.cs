using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Inventory.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Inventory.View.Widget
{
    [Widget("Style List", "Inventory", "A list of Styles.")]
    public partial class StyleListView
    {
        #region Constructors

        public StyleListView()
        {
            InitializeComponent();
        }

        public StyleListView(StyleListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
