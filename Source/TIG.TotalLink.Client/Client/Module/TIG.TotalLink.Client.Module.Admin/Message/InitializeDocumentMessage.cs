using System.ComponentModel;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;

namespace TIG.TotalLink.Client.Module.Admin.Message
{
    /// <summary>
    /// Initializes widgets immediately after a document is opened.
    /// Handled by any widget which is capable of initialization.
    /// </summary>
    [DisplayName("Initialize Document")]
    public class InitializeDocumentMessage : MessageBase
    {
        #region Constructors

        public InitializeDocumentMessage(object sender, object parameter)
            : base(sender)
        {
            Parameter = parameter;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Data that was passed with the message.
        /// </summary>
        public object Parameter { get; private set; }

        /// <summary>
        /// Returns a display title for this object.
        /// </summary>
        public string Title
        {
            get
            {
                var documentDataModel = Parameter as DocumentDataModelBase;
                if (documentDataModel != null)
                    return documentDataModel.Title;

                return ToString();
            }
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            if (Parameter != null)
                return Parameter.ToString();

            return base.ToString();
        }

        #endregion
    }
}
