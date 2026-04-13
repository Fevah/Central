using DevExpress.Xpf.Ribbon;
using TIG.TotalLink.ServerManager.ViewModel;

namespace TIG.TotalLink.ServerManager.Window
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : DXRibbonWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(MainWindowViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
