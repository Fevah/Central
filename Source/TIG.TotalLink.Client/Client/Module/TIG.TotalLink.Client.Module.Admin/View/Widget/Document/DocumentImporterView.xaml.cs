using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Document
{
    [Widget("Document Importer", "Document", "Imports a Document and Ribbon structure from a spreadsheet.")]
    public partial class DocumentImporterView : UserControl
    {
        public DocumentImporterView()
        {
            InitializeComponent();
        }

        public DocumentImporterView(DocumentImporterViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
