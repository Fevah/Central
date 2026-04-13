using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Ribbon;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Ribbon
{
    [Widget("Ribbon Group List", "Ribbon", "A list of Ribbon Groups.")]
    public partial class RibbonGroupListView : UserControl
    {
        #region Constructors

        public RibbonGroupListView()
        {
            InitializeComponent();
        }

        public RibbonGroupListView(RibbonGroupListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
