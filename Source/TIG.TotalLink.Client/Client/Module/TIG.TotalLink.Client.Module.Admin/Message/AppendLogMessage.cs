using System.ComponentModel;
using TIG.TotalLink.Client.Core.Message.Core;

namespace TIG.TotalLink.Client.Module.Admin.Message
{
    [DisplayName("Append Log")]
    public class AppendLogMessage : MessageBase
    {
        #region Constructors

        /// <summary>
        /// Message constructor.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="message">The message.</param>
        public AppendLogMessage(object sender, string message)
            : base(sender)
        {
            Message = message;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The widget message.
        /// </summary>
        public string Message { get; private set; }

        #endregion
    }
}
