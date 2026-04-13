using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Backstage;

namespace TIG.TotalLink.Client.Module.Admin.View.Backstage
{
    /// <summary>
    /// Interaction logic for ThemeGalleryView.xaml
    /// </summary>
    public partial class ThemeGalleryView : UserControl
    {
        public ThemeGalleryView()
        {
            InitializeComponent();
        }

        public ThemeGalleryView(ThemeGalleryViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
