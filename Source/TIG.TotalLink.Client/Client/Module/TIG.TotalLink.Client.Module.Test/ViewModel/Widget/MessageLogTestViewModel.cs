using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Test.ViewModel.Widget
{
    [SendsDocumentMessage(typeof(AppendLogMessage))]
    public class MessageLogTestViewModel : LocalDetailViewModelBase
    {
        #region Private Fields

        private string _message = "This is a test of the Message Log system.";

        #endregion


        #region Constructors

        public MessageLogTestViewModel()
        {
            // Initialize Commands
            SendMessageCommand = new DelegateCommand(OnSendMessageExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Sends the message.
        /// </summary>
        public ICommand SendMessageCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The message that will be sent.
        /// </summary>
        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value, () => Message, () => SendAppendLogMessage(Message)); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sends an AppendLogMessage.
        /// </summary>
        /// <param name="message">The message to send.</param>
        protected virtual void SendAppendLogMessage(string message)
        {
            SendDocumentMessage(new AppendLogMessage(this, message));
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the SendMessageCommand.
        /// </summary>
        private void OnSendMessageExecute()
        {
            SendAppendLogMessage(Message);
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<MessageLogTestViewModel> builder)
        {
            builder.DataFormLayout()
                .ContainsProperty(p => p.Message)
                .ContainsProperty(p => p.SendMessageCommand);

            builder.Property(p => p.SendMessageCommand).DisplayName("Send Message");
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<MessageLogTestViewModel> builder)
        {
            builder.Property(p => p.SendMessageCommand).HideLabel();
        }

        #endregion
    }
}
