using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Document
{
    [Widget("Panel List", "Document", "A list of Panels.")]
    public partial class PanelListView : UserControl
    {
        #region Constructors

        public PanelListView()
        {
            InitializeComponent();
        }

        public PanelListView(PanelListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
