using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Test.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Test.View.Widget
{
    [Widget("Test Object Importer", "Test", "Imports Test Objects from a spreadsheet.")]
    public partial class TestObjectImporterView
    {
        public TestObjectImporterView()
        {
            InitializeComponent();
        }

        public TestObjectImporterView(TestObjectImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
