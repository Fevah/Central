using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Document
{
    [Widget("Document Uploader", "Document", "Uploads a Document and Ribbon structure that was imported from a spreadsheet.")]
    public partial class DocumentUploaderView : UserControl
    {
        public DocumentUploaderView()
        {
            InitializeComponent();
        }

        public DocumentUploaderView(DocumentUploaderViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
