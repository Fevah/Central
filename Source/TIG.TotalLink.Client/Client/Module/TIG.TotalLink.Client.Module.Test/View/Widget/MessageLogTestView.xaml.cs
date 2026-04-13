using System.Windows.Controls;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Test.ViewModel.Widget;

namespace TIG.TotalLink.Client.Module.Test.View.Widget
{
    [Widget("Message Log Test", "Test", "Sends test messages to the Message Log widget.")]
    public partial class MessageLogTestView : UserControl
    {
        public MessageLogTestView()
        {
            InitializeComponent();
        }

        public MessageLogTestView(MessageLogTestViewModel viewModel)
            : this()
        {
            DataContext = viewModel;
        }
    }
}
