using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TIG.TotalLink.Client.Core.Message.Core
{
    [DisplayName("(All Messages)")]
    public abstract class MessageBase
    {
        #region Constructors

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        private MessageBase()
        {
            CreatedOn = DateTime.Now;
        }

        /// <summary>
        /// Constructor with sender.
        /// </summary>
        /// <param name="sender">The sender.</param>
        protected MessageBase(object sender)
            : this()
        {
            Sender = sender;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Message instantiation time.
        /// </summary>
        public DateTime CreatedOn { get; private set; }

        /// <summary>
        /// Message sender.
        /// </summary>
        public object Sender { get; private set; }

        /// <summary>
        /// A list of objects that can receive this message.
        /// </summary>
        public List<object> Receivers { get; set; }

        #endregion
    }
}
