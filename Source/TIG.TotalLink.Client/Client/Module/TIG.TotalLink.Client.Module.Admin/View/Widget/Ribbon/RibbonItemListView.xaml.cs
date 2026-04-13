using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Ribbon;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Ribbon
{
    [Widget("Ribbon Item List", "Ribbon", "A list of Ribbon Items.")]
    public partial class RibbonItemListView : UserControl
    {
        #region Constructors

        public RibbonItemListView()
        {
            InitializeComponent();
        }

        public RibbonItemListView(RibbonItemListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
