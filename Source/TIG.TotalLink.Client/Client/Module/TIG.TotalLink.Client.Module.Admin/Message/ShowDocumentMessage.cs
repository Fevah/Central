using System;
using System.Linq;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Message
{
    /// <summary>
    /// Shows a document.
    /// Always handled by MainViewModel.
    /// </summary>
    public class ShowDocumentMessage : MessageBase
    {
        #region Constructors

        public ShowDocumentMessage(object sender, Guid id, string name, object parameter, bool isFixed = false)
            : base(sender)
        {
            Id = id;
            Name = name;
            Parameter = parameter;
            IsFixed = isFixed;
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
        public string Name { get; private set; }

        /// <summary>
        /// Data that was passed with the message.
        /// Usually contains the parameter that was passed to the ShowDocumentCommand that triggered this message.
        /// </summary>
        public object Parameter { get; private set; }

        /// <summary>
        /// If the Id is not an empty guid, then we assume that the document needs to be loaded from the database.
        /// In this case, the Name will be null.
        /// </summary>
        public bool IsStored
        {
            get { return (Id != Guid.Empty); }
        }

        /// <summary>
        /// If the Id is an empty guid, then this message will create new blank document.
        /// In this case, the Name will not be null.
        /// </summary>
        public bool IsNew
        {
            get { return (Id == Guid.Empty); }
        }

        /// <summary>
        /// Indicates if the document will contain a fixed set of widgets.
        /// </summary>
        public bool IsFixed { get; private set; }

        /// <summary>
        /// Indicates if the document will be intialized with an InitializeDocumentMessage.
        /// </summary>
        public bool IsInitializedWithItem
        {
            get { return (Parameter is InitializeDocumentMessage); }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Sends a ShowDocumentMessage which will open a persistent Document.
        /// </summary>
        /// <param name="sender">The object which is sending the message.</param>
        /// <param name="document">The Document to show.</param>
        /// <param name="parameter">A parameter to send to the Document.</param>
        public static void Send(object sender, Document document, object parameter)
        {
            Messenger.Default.Send(new ShowDocumentMessage(sender, document.Oid, document.Name, parameter));
        }

        /// <summary>
        /// Sends a ShowDocumentMessage which will open a persistent Document.
        /// </summary>
        /// <param name="sender">The object which is sending the message.</param>
        /// <param name="id">The Oid of the Document to show.</param>
        /// <param name="name">A Name to use as the title of the Document.</param>
        /// <param name="parameter">A parameter to send to the Document.</param>
        /// <param name="isFixed">Indicates if the document will contain a fixed set of widgets.</param>
        public static void Send(object sender, Guid id, string name, object parameter, bool isFixed)
        {
            Messenger.Default.Send(new ShowDocumentMessage(sender, id, name, parameter, isFixed));
        }

        /// <summary>
        /// Sends a ShowDocumentMessage which will open a persistent Document via a DocumentAction.
        /// </summary>
        /// <param name="sender">The object which is sending the message.</param>
        /// <param name="documentActionName">The name of the DocumentAction to execute.</param>
        /// <param name="parameter">A parameter for initializing widgets within the Document.</param>
        public static void Send(object sender, string documentActionName, object parameter)
        {
            // Attempt to get the admin facade
            var adminFacade = DataObjectHelper.GetFacade<DocumentAction>();
            if (adminFacade == null)
                throw new Exception(string.Format("Failed to find facade to perform Document Action {0}.", documentActionName));

            // Attempt to get the DocumentAction
            var documentAction = adminFacade.ExecuteQuery(uow =>
                uow.Query<DocumentAction>().Where(d => d.Name == documentActionName)
            ).FirstOrDefault();
            if (documentAction == null)
                throw new Exception(string.Format("Failed to find Document Action {0}.", documentActionName));

            // Send a ShowDocumentMessage, containing an InitializeDocumentMessage
            Send(sender, documentAction.Document, new InitializeDocumentMessage(sender, parameter));
        }

        #endregion
    }
}
