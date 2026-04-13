using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Test.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Test.View.Widget
{
    [Widget("Test Object Uploader", "Test", "Uploads Test Objects that were imported from a spreadsheet.")]
    public partial class TestObjectUploaderView
    {
        public TestObjectUploaderView()
        {
            InitializeComponent();
        }

        public TestObjectUploaderView(TestObjectUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
