using System;
using DevExpress.Mvvm;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler
{
    public class SchedulerItemViewModel : ViewModelBase
    {
        #region Staff

        private DateTime _start;
        private DateTime _endTime;
        private bool _allDay;
        private string _subject;
        private string _description;
        private string _location;
        private int _label;
        private int _status;
        private string _reminderInfo;
        private int _eventType;
        private Guid _id;
        private int _resourceId;
        private string _recurrenceInfo;

        #endregion


        #region Public Properites

        /// <summary>
        /// Id for unique this object
        /// </summary>
        public Guid Oid
        {
            get { return _id; }
            set
            {
                _id = value;
                RaisePropertyChanged("Oid");
            }
        }

        /// <summary>
        /// EventType for event type
        /// </summary>
        public int EventType
        {
            get { return _eventType; }
            set
            {
                _eventType = value;
                RaisePropertyChanged("EventType");
            }
        }

        /// <summary>
        /// Start for start date
        /// </summary>
        public DateTime StartDate
        {
            get { return _start; }
            set
            {
                _start = value;
                RaisePropertyChanged("StartDate");
            }
        }

        /// <summary>
        /// End for end date
        /// </summary>
        public DateTime EndDate
        {
            get { return _endTime; }
            set
            {
                _endTime = value;
                RaisePropertyChanged("EndDate");
            }
        }

        /// <summary>
        /// All Day for mark can edit time or not
        /// </summary>
        public bool AllDay
        {
            get { return _allDay; }
            set
            {
                _allDay = value;
                RaisePropertiesChanged("AllDay");
            }
        }

        /// <summary>
        /// Subject for Appointment subject
        /// </summary>
        public string Subject
        {
            get { return _subject; }
            set
            {
                _subject = value;
                RaisePropertiesChanged("Subject");
            }
        }

        /// <summary>
        /// Description for appointment description
        /// </summary>
        public string Description
        {
            get { return _description; }
            set
            {
                _description = value;
                RaisePropertiesChanged("Description");
            }
        }

        /// <summary>
        /// Location for where it is
        /// </summary>
        public string Location
        {
            get { return _location; }
            set
            {
                _location = value;
                RaisePropertiesChanged("Location");
            }
        }

        /// <summary>
        /// Label for mark for UI element color
        /// </summary>
        public int Label
        {
            get { return _label; }
            set
            {
                _label = value;
                RaisePropertiesChanged("Label");
            }
        }

        /// <summary>
        /// What status for appointment
        /// </summary>
        public int Status
        {
            get { return _status; }
            set
            {
                _status = value;
                RaisePropertiesChanged("Status");
            }
        }


        /// <summary>
        /// Link to Resource
        /// </summary>
        public int ResourceId
        {
            get { return _resourceId; }
            set
            {
                _resourceId = value;
                RaisePropertyChanged("ResourceId");
            }
        }

        /// <summary>
        /// RecurreceInfo for store recurrence information
        /// </summary>
        public string RecurrenceInfo
        {
            get { return _recurrenceInfo; }
            set
            {
                _recurrenceInfo = value;
                RaisePropertyChanged("RecurrenceInfo");
            }
        }

        /// <summary>
        /// RemiderInfo for Remider
        /// </summary>
        public string ReminderInfo
        {
            get { return _reminderInfo; }
            set
            {
                _reminderInfo = value;
                RaisePropertyChanged("ReminderInfo");
            }
        }

        #endregion
    }
}
