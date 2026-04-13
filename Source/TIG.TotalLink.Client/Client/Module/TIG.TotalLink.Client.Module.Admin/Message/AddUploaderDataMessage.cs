using System.Collections;
using System.ComponentModel;
using TIG.TotalLink.Client.Core.Message.Core;

namespace TIG.TotalLink.Client.Module.Admin.Message
{
    [DisplayName("Add Uploader Data")]
    public class AddUploaderDataMessage : MessageBase
    {
        #region Constructors

        public AddUploaderDataMessage(object sender, IEnumerable data)
            : base(sender)
        {
            Data = data;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of uploader data objects to add.
        /// </summary>
        public IEnumerable Data { get; private set; }

        #endregion
    }
}
