using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Test.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Test.View.Widget
{
    [Widget("Test View Model List", "Test", "A list of Test View Models.  This list is for testing that grids can manage non-persistent data.")]
    public partial class TestViewModelListView : UserControl
    {
        #region Constructors

        public TestViewModelListView()
        {
            InitializeComponent();
        }

        public TestViewModelListView(TestViewModelListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
