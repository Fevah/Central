using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Debug;

namespace TIG.TotalLink.Client.Module.Admin.View.Widget.Debug
{
    [Widget("Message Monitor", "Debug", "Displays all internal document related messages.")]
    public partial class MessageMonitorListView : UserControl
    {
        #region Constructors

        public MessageMonitorListView()
        {
            InitializeComponent();
        }

        public MessageMonitorListView(MessageMonitorListViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }

        #endregion
    }
}
