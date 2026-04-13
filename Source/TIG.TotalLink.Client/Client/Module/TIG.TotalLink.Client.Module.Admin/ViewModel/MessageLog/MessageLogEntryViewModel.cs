using System;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Module.Admin.Message;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.MessageLog
{
    public class MessageLogEntryViewModel : ViewModelBase
    {
        #region Private Fields

        private DateTime _createdOn;
        private string _message;

        #endregion


        #region Constructors

        /// <summary>
        /// Parameterless constructor
        /// </summary>
        public MessageLogEntryViewModel()
        {
        }

        /// <summary>
        /// Comstructs the instance via AppendLogMessage instance.
        /// </summary>
        /// <param name="message">The AppendLogMessage message.</param>
        public MessageLogEntryViewModel(AppendLogMessage message)
            :this()
        {
            CreatedOn = message.CreatedOn;
            Message = message.Message;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Moment of time when the message was created.
        /// </summary>
        public DateTime CreatedOn
        {
            get { return _createdOn; }
            set { SetProperty(ref _createdOn, value, () => CreatedOn); }
        }

        /// <summary>
        /// The widget message.
        /// </summary>
        public string Message
        {
            get { return _message; }
            set { SetProperty(ref _message, value, () => Message); }
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format(
                "{0:G} : {1}",
                CreatedOn,
                Message
                );
        }

        #endregion
    }
}
