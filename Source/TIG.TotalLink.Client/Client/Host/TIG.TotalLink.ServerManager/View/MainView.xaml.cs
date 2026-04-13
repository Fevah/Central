using System.Windows.Controls;
using TIG.TotalLink.ServerManager.ViewModel;

namespace TIG.TotalLink.ServerManager.View
{
    /// <summary>
    /// Interaction logic for MainView.xaml
    /// </summary>
    public partial class MainView : UserControl
    {

        #region Constructors

        public MainView()
        {
            InitializeComponent();
        }

        public MainView(MainViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
