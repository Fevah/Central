using DevExpress.XtraScheduler;
using TIG.TotalLink.Client.Module.Admin.Message;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler
{
    public class SchedulerDetailViewModel : WidgetViewModelBase
    {
        #region Private Properties

        private Appointment _selectedItem;

        #endregion


        #region Public Properties
        
        /// <summary>
        /// Selected Appointment
        /// </summary>
        public Appointment SelectedItem
        {
            get { return _selectedItem; }
            set { SetProperty(ref _selectedItem, value, () => SelectedItem); }
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Register Document Message
        /// </summary>
        /// <param name="documentId">Current document Id</param>
        protected override void OnRegisterDocumentMessages(object documentId)
        {
            base.OnRegisterDocumentMessages(documentId);

            RegisterDocumentMessage<SelectedAppointmentChangedMessage>(documentId, OnSelectedItemChangedMessage);
        }

        /// <summary>
        /// UnRegister Document Message
        /// </summary>
        /// <param name="documentId">Current document Id</param>
        protected override void OnUnregisterDocumentMessages(object documentId)
        {
            base.OnUnregisterDocumentMessages(documentId);

            UnregisterDocumentMessage<SelectedAppointmentChangedMessage>(documentId);
        }

        #endregion


        #region Private Method

        /// <summary>
        /// Event Handler for selected appointment change message.
        /// </summary>
        /// <param name="message"></param>
        private void OnSelectedItemChangedMessage(SelectedAppointmentChangedMessage message)
        {
            SelectedItem = message.SelectedItem as Appointment;
        }

        #endregion
    }
}
