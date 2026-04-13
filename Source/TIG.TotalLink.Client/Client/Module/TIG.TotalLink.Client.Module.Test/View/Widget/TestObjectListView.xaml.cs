using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Test.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Test.View.Widget
{
    [Widget("Test Object List", "Test", "A list of Test Objects.  This list is for testing that grids can manage persistent data, and displays all available custom editors.")]
    public partial class TestObjectListView : UserControl
    {
        #region Constructors

        public TestObjectListView()
        {
            InitializeComponent();
        }

        public TestObjectListView(TestObjectListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
