using System.ComponentModel;
using TIG.TotalLink.Client.Core.Message.Core;

namespace TIG.TotalLink.Client.Module.Admin.Message
{
    /// <summary>
    /// Notifies widgets that the selected items have changed in the active widget.
    /// Can be handled by any widget that needs to be aware of which items are selected in other widgets.
    /// </summary>
    [DisplayName("Selected Appointments Changed")]
    public class SelectedAppointmentChangedMessage : MessageBase
    {
        #region Constructors

        public SelectedAppointmentChangedMessage(object sender, object selectedItem)
            : base(sender)
        {
            SelectedItem = selectedItem;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// All items that are currently selected.
        /// </summary>
        public object SelectedItem { get; private set; }

        #endregion
    }
}
