using DevExpress.Xpf.Ribbon;
using TIG.TotalLink.Client.ViewModel;

namespace TIG.TotalLink.Client.Window
{
    public partial class LoginWindow : DXRibbonWindow
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        public LoginWindow(LoginWindowViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
