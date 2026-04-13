using System;
using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Editor.Builder;
using TIG.TotalLink.Client.Editor.Definition;
using TIG.TotalLink.Client.Undo.Extension;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.MessageMonitor
{
    public class MessageMonitorEntryViewModel : ViewModelBase
    {
        #region Private Fields

        private DateTime _createdOn;
        private DateTime _receivedOn;
        private string _messageTypeName;
        private string _senderTypeName;
        private string _messageContent;

        #endregion


        #region Constructors

        public MessageMonitorEntryViewModel()
        {
            ReceivedOn = DateTime.Now;
        }

        public MessageMonitorEntryViewModel(MessageBase message)
            : this()
        {
            CreatedOn = message.CreatedOn;
            MessageTypeName = message.GetType().Name;
            SenderTypeName = message.Sender.GetType().Name;
            MessageContent = message.SerializeToJson();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The time that the message was created.
        /// </summary>
        public DateTime CreatedOn
        {
            get { return _createdOn; }
            set { SetProperty(ref _createdOn, value, () => CreatedOn); }
        }

        /// <summary>
        /// The time that the message was received by the monitor.
        /// </summary>
        public DateTime ReceivedOn
        {
            get { return _receivedOn; }
            set { SetProperty(ref _receivedOn, value, () => ReceivedOn); }
        }

        /// <summary>
        /// Full type name of the message.
        /// </summary>
        public string MessageTypeName
        {
            get { return _messageTypeName; }
            set { SetProperty(ref _messageTypeName, value, () => MessageTypeName); }
        }

        /// <summary>
        /// Full type name of the message sender.
        /// </summary>
        public string SenderTypeName
        {
            get { return _senderTypeName; }
            set { SetProperty(ref _senderTypeName, value, () => SenderTypeName); }
        }

        /// <summary>
        /// The entire message serailized as a Json string.
        /// </summary>
        public string MessageContent
        {
            get { return _messageContent; }
            set { SetProperty(ref _messageContent, value, () => MessageContent); }
        }

        #endregion

        
        #region Overrides

        public override string ToString()
        {
            return MessageTypeName;
        }

        #endregion


        #region Metadata

        /// <summary>
        /// Builds metadata for properties.
        /// </summary>
        /// <param name="builder">The MetadataBuilder for defining property metadata.</param>
        public static void BuildMetadata(MetadataBuilder<MessageMonitorEntryViewModel> builder)
        {
            builder.Group("")
                .ContainsProperty(p => p.ReceivedOn)
                .ContainsProperty(p => p.CreatedOn)
                .ContainsProperty(p => p.MessageTypeName)
                .ContainsProperty(p => p.SenderTypeName)
                .ContainsProperty(p => p.MessageContent);

            builder.Property(p => p.ReceivedOn)
                .ReadOnly();
            builder.Property(p => p.CreatedOn)
                .ReadOnly();
            builder.Property(p => p.MessageTypeName)
                .DisplayName("Message Type")
                .ReadOnly();
            builder.Property(p => p.SenderTypeName)
                .DisplayName("Sender Type")
                .ReadOnly();
            builder.Property(p => p.MessageContent)
                .DisplayName("Content")
                .ReadOnly();
        }

        /// <summary>
        /// Builds metadata for editors.
        /// </summary>
        /// <param name="builder">The EditorMetadataBuilder for defining editor metadata.</param>
        public static void BuildEditorMetadata(EditorMetadataBuilder<MessageMonitorEntryViewModel> builder)
        {
            builder.Sort()
                .ContainsProperty(p => p.ReceivedOn);

            builder.Property(p => p.ReceivedOn).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
            builder.Property(p => p.CreatedOn).GetEditor<DateTimeEditorDefinition>().ShowTime = true;
            builder.Property(p => p.MessageContent).ReplaceEditor(new MemoEditorDefinition());

            builder.Property(p => p.MessageContent).UnlimitedLength();
        }

        #endregion
    }
}
