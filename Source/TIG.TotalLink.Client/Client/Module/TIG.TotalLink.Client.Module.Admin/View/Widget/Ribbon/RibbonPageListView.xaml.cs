using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Ribbon;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Ribbon
{
    [Widget("Ribbon Page List", "Ribbon", "A list of Ribbon Pages.")]
    public partial class RibbonPageListView : UserControl
    {
        #region Constructors

        public RibbonPageListView()
        {
            InitializeComponent();
        }

        public RibbonPageListView(RibbonPageListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
