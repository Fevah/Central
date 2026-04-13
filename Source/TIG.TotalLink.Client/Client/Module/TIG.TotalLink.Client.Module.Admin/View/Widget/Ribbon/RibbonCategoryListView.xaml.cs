using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Ribbon;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Ribbon
{
    [Widget("Ribbon Category List", "Ribbon", "A list of Ribbon Categories.")]
    public partial class RibbonCategoryListView : UserControl
    {
        #region Constructors

        public RibbonCategoryListView()
        {
            InitializeComponent();
        }

        public RibbonCategoryListView(RibbonCategoryListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
