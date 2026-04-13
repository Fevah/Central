using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Server;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Server
{
    [Widget("Sequence List", "Server", "A list of Sequences.")]
    public partial class SequenceListView
    {
        #region Constructors

        public SequenceListView()
        {
            InitializeComponent();
        }

        public SequenceListView(SequenceListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
