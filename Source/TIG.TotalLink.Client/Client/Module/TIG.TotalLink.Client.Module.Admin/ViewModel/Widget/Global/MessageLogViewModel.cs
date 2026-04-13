using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.MessageLog;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global
{
    public class MessageLogViewModel : WidgetViewModelBase
    {
        #region Private Fields

        private readonly ObservableCollection<MessageLogEntryViewModel> _logEntries = new ObservableCollection<MessageLogEntryViewModel>();
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private string _logString;

        #endregion


        #region Constructors

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public MessageLogViewModel()
        {
            _logEntries.CollectionChanged += LogEntries_CollectionChanged;

            //Commands setup
            ClearLogCommand = new DelegateCommand(OnClearLogExecute, OnClearLogCanExecute);
        }

        #endregion


        #region Commands

        /// <summary>
        /// Clears the log.
        /// </summary>
        [WidgetCommand("Clear Log", "Edit", RibbonItemType.ButtonItem, "Clear the message log.")]
        public ICommand ClearLogCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// String representation of the log entries.
        /// </summary>
        public string LogString
        {
            get { return _logString; }
            private set { SetProperty(ref _logString, value, () => LogString); }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Creates new log entry.
        /// </summary>
        /// <param name="message">The message.</param>
        private void OnAppendLogMessage(AppendLogMessage message)
        {
            var logEntry = new MessageLogEntryViewModel(message);
            _logEntries.Add(logEntry);
        }

        /// <summary>
        /// Handles the CollectionChanged event on the logEntries collection.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LogEntries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (MessageLogEntryViewModel logEntry in e.NewItems)
                    {
                        AppendLogEntry(logEntry);
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    throw new NotSupportedException();

                case NotifyCollectionChangedAction.Reset:
                    ClearLogEntries();
                    break;
            }
        }

        /// <summary>
        /// Execute method for ClearLogCommand.
        /// </summary>
        private void OnClearLogExecute()
        {
            _logEntries.Clear();
        }

        /// <summary>
        /// CanExecute method for ClearLogCommand.
        /// </summary>
        private bool OnClearLogCanExecute()
        {
            if (_logEntries == null || _logEntries.Count == 0)
                return false;

            return CanExecuteWidgetCommand;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Appends an entry to the LogString.
        /// </summary>
        /// <param name="logEntry">The log entry to append.</param>
        private void AppendLogEntry(MessageLogEntryViewModel logEntry)
        {
            _logBuilder.AppendLine(logEntry.ToString());
            LogString = _logBuilder.ToString();
        }

        /// <summary>
        /// Clears the LogString.
        /// </summary>
        private void ClearLogEntries()
        {
            _logBuilder.Clear();
            LogString = string.Empty;
        }

        #endregion


        #region Overrides

        protected override void OnRegisterDocumentMessages(object documentId)
        {
            base.OnRegisterDocumentMessages(documentId);

            RegisterDocumentMessage<AppendLogMessage>(documentId, OnAppendLogMessage);
        }

        protected override void OnUnregisterDocumentMessages(object documentId)
        {
            base.OnUnregisterDocumentMessages(documentId);

            UnregisterDocumentMessage<AppendLogMessage>(documentId);
        }

        #endregion

    }
}
