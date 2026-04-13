using System;
using System.Windows.Input;
using TIG.TotalLink.Client.Module.Admin.Message;

namespace TIG.TotalLink.Client.Module.Admin.Command
{
    /// <summary>
    /// Shows a document by sending a ShowDocumentMessage.
    /// </summary>
    public class ShowDocumentCommand : ICommand
    {
        #region Private Fields

        private static int _nextDocumentNumber;

        #endregion


        #region Constructors

        public ShowDocumentCommand()
        {
        }

        public ShowDocumentCommand(string parameter)
            : this()
        {
            // Abort if the parameter is empty
            if (string.IsNullOrWhiteSpace(parameter))
                return;

            // Split the parameter
            var parameterParts = parameter.Split('|');
            if (parameterParts.Length > 0)
                Name = parameterParts[0];
            if (parameterParts.Length > 1)
                IsFixed = true;
        }

        public ShowDocumentCommand(Guid id)
            : this()
        {
            Id = id;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Id of the document to load.
        /// </summary>
        public Guid Id { get; private set; }

        /// <summary>
        /// Name of the document.
        /// Will only be applied if a new document is created.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// If the Id is not an empty guid, then we assume that the document needs to be loaded from the database.
        /// In this case, the Name will be null.
        /// </summary>
        public bool IsStored
        {
            get { return (Id != Guid.Empty); }
        }

        /// <summary>
        /// If the Id is an empty guid, then this command will create new blank document.
        /// In this case, the Name may contain a name for the new document.
        /// </summary>
        public bool IsNew
        {
            get { return (Id == Guid.Empty); }
        }

        /// <summary>
        /// Indicates if the document will contain one fixed widget.
        /// </summary>
        public bool IsFixed { get; private set; }

        #endregion


        #region ICommand

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            // Collect or generate the name and id based on the IsNew flag
            var name = IsNew && string.IsNullOrWhiteSpace(Name) ? string.Format("Document{0}", ++_nextDocumentNumber) : Name;

            // Send a ShowDocumentMessage
            ShowDocumentMessage.Send(this, Id, name, parameter, IsFixed);
        }

        #endregion

    }
}
