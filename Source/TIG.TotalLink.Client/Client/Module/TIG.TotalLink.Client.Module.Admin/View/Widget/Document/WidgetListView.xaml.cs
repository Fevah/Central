using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Document
{
    [Widget("Widget List", "Document", "A list of all available Widgets.")]
    public partial class WidgetListView : UserControl
    {
        #region Constructors

        public WidgetListView()
        {
            InitializeComponent();
        }

        public WidgetListView(WidgetListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
