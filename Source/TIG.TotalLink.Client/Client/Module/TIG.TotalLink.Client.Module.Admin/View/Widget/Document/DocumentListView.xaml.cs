using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Document
{
    [Widget("Document List", "Document", "A list of Documents.")]
    public partial class DocumentListView : UserControl
    {
        #region Constructors

        public DocumentListView()
        {
            InitializeComponent();
        }

        public DocumentListView(DocumentListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
