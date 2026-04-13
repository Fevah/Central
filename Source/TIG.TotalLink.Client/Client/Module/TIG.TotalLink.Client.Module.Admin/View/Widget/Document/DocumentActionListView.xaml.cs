using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Document;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Document
{
    [Widget("Document Action List", "Document", "A list of Document Actions.")]
    public partial class DocumentActionListView : UserControl
    {
        #region Constructors

        public DocumentActionListView()
        {
            InitializeComponent();
        }

        public DocumentActionListView(DocumentActionListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
