using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Enum;
using TIG.TotalLink.Client.Module.Global.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Global.View.Widget
{
    [HideWidget(HostTypes.Client)]
    [Widget("Main Database", "Server", "Configures the main database connection.")]
    public partial class MainDatabaseView : UserControl
    {
        public MainDatabaseView()
        {
            InitializeComponent();
        }

        public MainDatabaseView(MainDatabaseViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
