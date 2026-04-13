using System.ComponentModel;
using TIG.TotalLink.Client.Core.Message;

namespace TIG.TotalLink.Client.Core.Helper
{
    public class SelectedItemsChangingEventArgs : HandledEventArgs
    {
        #region Public Properties

        /// <summary>
        /// The SelectedItemsChangedMessage that triggered this event.
        /// </summary>
        public SelectedItemsChangedMessage SelectedItemsChangedMessage { get; private set; }

        #endregion


        #region Constructors

        public SelectedItemsChangingEventArgs(SelectedItemsChangedMessage selectedItemsChangedMessage)
        {
            SelectedItemsChangedMessage = selectedItemsChangedMessage;
        }

        #endregion
    }
}
