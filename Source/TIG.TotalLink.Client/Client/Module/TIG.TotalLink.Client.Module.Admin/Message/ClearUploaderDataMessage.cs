using System.ComponentModel;
using TIG.TotalLink.Client.Core.Message.Core;

namespace TIG.TotalLink.Client.Module.Admin.Message
{
    [DisplayName("Clear Uploader Data")]
    public class ClearUploaderDataMessage : MessageBase
    {
        #region Constructors

        public ClearUploaderDataMessage(object sender)
            : base(sender)
        {
        }

        #endregion
    }
}
