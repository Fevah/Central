using System;

namespace TIG.TotalLink.Client.Module.Admin.Attribute
{
    /// <summary>
    /// Registers that a widget sends a particular document message.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SendsDocumentMessageAttribute : System.Attribute
    {
        #region Constructors

        public SendsDocumentMessageAttribute(Type messageType)
        {
            MessageType = messageType;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The type of message that is sent.
        /// </summary>
        public Type MessageType { get; private set; }

        #endregion
    }
}
