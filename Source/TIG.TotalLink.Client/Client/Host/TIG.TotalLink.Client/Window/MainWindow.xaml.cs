using DevExpress.Xpf.Ribbon;
using TIG.TotalLink.Client.ViewModel;

namespace TIG.TotalLink.Client.Window
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
