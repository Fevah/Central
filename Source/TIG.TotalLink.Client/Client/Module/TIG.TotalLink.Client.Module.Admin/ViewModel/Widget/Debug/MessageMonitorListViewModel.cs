using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.MessageMonitor;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Debug
{
    public class MessageMonitorListViewModel : ListViewModelBase<MessageMonitorEntryViewModel>
    {
        #region Private Fields

        private readonly ObservableCollection<MessageMonitorEntryViewModel> _messages = new ObservableCollection<MessageMonitorEntryViewModel>();

        #endregion



        #region Constructors

        public MessageMonitorListViewModel()
        {
            // Initialize commands
            ClearMesssagesCommand = new DelegateCommand(OnClearMesssagesExecute, OnClearMesssagesCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Clears the messages list.
        /// </summary>
        [WidgetCommand("Clear Monitor", "Edit", RibbonItemType.ButtonItem, "Clear all messages from the Message Monitor.")]
        public ICommand ClearMesssagesCommand { get; private set; }

        /// <summary>
        /// Override to hide the AddCommand.
        /// </summary>
        public override ICommand AddCommand { get { return null; } }

        /// <summary>
        /// Override to hide the DeleteCommand.
        /// </summary>
        public override ICommand DeleteCommand { get { return null; } }

        /// <summary>
        /// Override to hide the RefreshCommand.
        /// </summary>
        public override ICommand RefreshCommand { get { return null; } }

        #endregion


        #region Public Properties

        /// <summary>
        /// All messages.
        /// </summary>
        public ObservableCollection<MessageMonitorEntryViewModel> Messages
        {
            get { return _messages; }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Creates new entry.
        /// </summary>
        /// <param name="message">The message.</param>
        private void OnMessageBaseMessage(MessageBase message)
        {
            // Ignore messages from this widget
            if (ReferenceEquals(message.Sender, this))
                return;

            var messageMonitorEntry = new MessageMonitorEntryViewModel(message);
            _messages.Add(messageMonitorEntry);
        }

        /// <summary>
        /// Execute method for ClearMesssagesCommand.
        /// </summary>
        private void OnClearMesssagesExecute()
        {
            _messages.Clear();
        }

        /// <summary>
        /// CanExecute method for ClearMesssagesCommand.
        /// </summary>
        private bool OnClearMesssagesCanExecute()
        {
            if (_messages == null || _messages.Count == 0)
                return false;

            return CanExecuteWidgetCommand;
        }

        #endregion


        #region Overrides

        protected override void OnWidgetLoaded(EventArgs e)
        {
            base.OnWidgetLoaded(e);

            AddStartupTask(() =>
            {
                // Initialize the data source
                ItemsSource = _messages;
            });
        }

        protected override void OnRegisterDocumentMessages(object documentId)
        {
            base.OnRegisterDocumentMessages(documentId);

            RegisterDocumentMessage<MessageBase>(documentId, OnMessageBaseMessage);
        }

        protected override void OnUnregisterDocumentMessages(object documentId)
        {
            base.OnUnregisterDocumentMessages(documentId);

            UnregisterDocumentMessage<MessageBase>(documentId);
        }

        #endregion
    }
}
