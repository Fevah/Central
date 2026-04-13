using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Global.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Global.View.Widget
{
    [HideWidget(HostTypes.Client)]
    [Widget("XpoProvider List", "Global", "A list of Xpo Providers.")]
    public partial class XpoProviderListView : UserControl
    {
        #region Constructors

        public XpoProviderListView()
        {
            InitializeComponent();
        }

        public XpoProviderListView(XpoProviderListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
