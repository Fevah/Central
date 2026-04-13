using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DevExpress.Mvvm;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Core.Native;
using DevExpress.Xpf.Docking;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Scheduler;
using DevExpress.Xpf.Scheduler.UI;
using DevExpress.XtraScheduler;
using DevExpress.XtraScheduler.Commands;
using DevExpress.XtraScheduler.Localization;
using DevExpress.XtraScheduler.UI;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler;
using Application = System.Windows.Forms.Application;
using TIG.TotalLink.Client.Module.Admin.Attribute;

namespace TIG.TotalLink.Client.Module.Admin.View.Core.Scheduler
{
    [Widget("Scheduler Detail", "Scheduler", "Displays details of Appointments selected in the Calendar.")]
    public partial class SchedulerDetailView : INotifyPropertyChanged
    {
        #region Private Properties

        private AppointmentFormController _appointmentVisualController;
        private RecurrenceVisualController _recurrenceVisualController;
        private bool _isEditSerise;

        #endregion


        #region Dependency Properties

        /// <summary>
        /// Dependency property for appointment
        /// </summary>
        public static readonly DependencyProperty AppointmentProperty = DependencyProperty.Register(
            "Appointment", typeof(Appointment), typeof(SchedulerDetailView), new PropertyMetadata(null, OnAppointmentChanged));

        /// <summary>
        /// Select Appointment
        /// </summary>
        public Appointment Appointment
        {
            get { return (Appointment)GetValue(AppointmentProperty); }
            set { SetValue(AppointmentProperty, value); }
        }

        #endregion


        #region Properties Changed

        /// <summary>
        /// On Appointment change method
        /// </summary>
        /// <param name="sender">Scheduler Item view</param>
        /// <param name="e">Message for old value and new value</param>
        private static void OnAppointmentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var editor = sender as SchedulerDetailView;
            if (editor == null) { return; }

            //If there are no more item be selected we will disable view
            if (e.OldValue != null
                && e.NewValue == null)
            {
                editor.IsEnabled = false;
            }

            var appointment = e.NewValue as Appointment;
            if (appointment == null) { return; }
            //Any appointment has been selected, view will be enable
            editor.IsEnabled = true;

            if (editor.Scheduler == null) { return; }

            //According to selected appointment to initialize appointment controller and resource controller 
            editor.AppointmentVisualController = new AppointmentFormController(editor.Scheduler, appointment);
            editor.RecurrenceVisualController = new RecurrenceVisualController(editor.AppointmentVisualController);

            editor.EditSeriesBarItem.IsEnabled = appointment.IsRecurring;

            if (appointment.Type != AppointmentType.Pattern)
            {
                editor.EditOccurrenceBarItem.IsChecked = true;
            }
            else
            {
                editor.EditSeriesBarItem.IsChecked = true;
            }
        }

        #endregion


        #region Properites

        /// <summary>
        /// Parent scheduler control
        /// </summary>
        public SchedulerControl Scheduler { get; private set; }

        /// <summary>
        /// TimeEditMask will be according to running environment to get time pattern and it will be use for binding to time control
        /// </summary>
        public string TimeEditMask { get { return CultureInfo.CurrentCulture.DateTimeFormat.LongTimePattern; } }

        /// <summary>
        /// AppointmentVisualController packing a appointment object, use for binding data to UI part.
        /// </summary>
        public AppointmentFormController AppointmentVisualController
        {
            get { return _appointmentVisualController; }
            set
            {
                if (_appointmentVisualController == value) { return; }
                _appointmentVisualController = value;
                OnPropertyChanged("AppointmentVisualController");
            }
        }

        /// <summary>
        /// RecurrenceVisualController packing a appointment object, use for binding data to UI part.
        /// </summary>
        public RecurrenceVisualController RecurrenceVisualController
        {
            get { return _recurrenceVisualController; }
            set
            {
                _recurrenceVisualController = value;
                OnPropertyChanged("RecurrenceVisualController");
            }
        }

        /// <summary>
        /// Save Command
        /// </summary>
        public ICommand SaveCommand
        {
            get { return new DelegateCommand(SaveAppointment); }
        }

        /// <summary>
        /// Cancel Command
        /// </summary>
        public ICommand CancelCommand
        {
            get { return new DelegateCommand(CancelAppointment); }
        }

        /// <summary>
        /// IsEditSerise will be binding to UI part for EditSerise bar button
        /// </summary>
        public bool IsEditSerise
        {
            get { return _isEditSerise; }
            set
            {
                _isEditSerise = value;
                OnPropertyChanged("IsEditSerise");
            }
        }

        /// <summary>
        /// IsEditOccurrence will be binding to UI part for IsEditOccurrence bar button
        /// </summary>
        public bool IsEditOccurrence
        {
            get { return !_isEditSerise; }
            set
            {
                _isEditSerise = !value;
                OnPropertyChanged("IsEditOccurrence");
            }
        }

        //private const string IconPath = @"pack://application:,,,/Images/";

        ///// <summary>
        ///// Icon for edit Occurrence
        ///// </summary>
        //public BitmapFrame IconForOccurrence
        //{
        //    get
        //    {
        //        return BitmapFrame.Create(new Uri(
        //            string.Format(@"{0}Calendar_Select_Day.png", IconPath), UriKind.Absolute));
        //    }
        //}

        /// <summary>
        /// Icon for edit series
        /// </summary>
        //public BitmapFrame IconForSeries
        //{
        //    get
        //    {
        //        return BitmapFrame.Create(new Uri(
        //            string.Format(@"{0}Calendar_Dates_Adjust.png", IconPath), UriKind.Absolute));
        //    }
        //}

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        public SchedulerDetailView()
        {
            InitializeComponent();
            IsEnabled = false;
            Loaded += (s, e) =>
            {
                var documentPanel = LayoutHelper.FindLayoutOrVisualParentObject<DocumentPanel>(this);
                if (documentPanel != null)
                    Scheduler = LayoutHelper.FindElementByType<SchedulerControl>(documentPanel);
            };
        }

        /// <summary>
        /// Constructor with ViewModel
        /// </summary>
        /// <param name="viewModel"></param>
        public SchedulerDetailView(SchedulerDetailViewModel viewModel)
            : this()
        {
            var binding = new Binding
            {
                Path = new PropertyPath("SelectedItem"),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            BindingOperations.SetBinding(this, AppointmentProperty, binding);

            DataContext = viewModel;
        }

        #endregion


        #region INotifyPropertyChanged

        /// <summary>
        /// INotifyPropertyChanged interface
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notification to register
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion


        #region Event handlers

        /// <summary>
        /// EditEnd Time event handler
        /// </summary>
        /// <param name="sender">EndTime Control</param>
        /// <param name="e">Validation event args</param>
        private void OnEditEndTimeValidate(object sender, ValidationEventArgs e)
        {
            if (e.Value == null)
                return;
            e.IsValid = IsValidInterval(AppointmentVisualController.Start.Date, AppointmentVisualController.Start.TimeOfDay, AppointmentVisualController.End.Date, ((DateTime)e.Value).TimeOfDay);
            e.ErrorContent = SchedulerLocalizer.GetString(SchedulerStringId.Msg_InvalidEndDate);
        }

        /// <summary>
        /// EditEnd Data event handler
        /// </summary>
        /// <param name="sender">EndDate Control</param>
        /// <param name="e">Validation event args</param>
        private void OnEdtEndDateValidate(object sender, ValidationEventArgs e)
        {
            if (e.Value == null)
                return;
            e.IsValid = IsValidInterval(AppointmentVisualController.Start.Date, AppointmentVisualController.Start.Date.TimeOfDay, (DateTime)e.Value, AppointmentVisualController.End.TimeOfDay);
            e.ErrorContent = SchedulerLocalizer.GetString(SchedulerStringId.Msg_InvalidEndDate);
        }

        /// <summary>
        /// Real validation methon, just invoke base static method to validate this value
        /// </summary>
        /// <param name="startDate">Start Date</param>
        /// <param name="startTime">Start Time</param>
        /// <param name="endDate">End Date</param>
        /// <param name="endTime">End Time</param>
        /// <returns></returns>
        private bool IsValidInterval(DateTime startDate, TimeSpan startTime, DateTime endDate, TimeSpan endTime)
        {
            return AppointmentFormControllerBase.ValidateInterval(startDate, startTime, endDate, endTime);
        }

        /// <summary>
        /// On edit serise baritem checked
        /// We will trigger scheduler editserise command
        /// </summary>
        /// <param name="sender">edit serise baritem</param>
        /// <param name="e">message for checked changed</param>
        private void IsSerise_OnCheckedChanged(object sender, ItemClickEventArgs e)
        {
            if (!IsEditSerise) return;
            var editSeriseCommand = Scheduler.CreateCommand(SchedulerCommandId.EditSeriesUI);
            editSeriseCommand.Execute();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// SaveAppointment
        /// </summary>
        private void SaveAppointment()
        {
            //Any validation error, app will cancel save. 
            if (EdtEndDate.HasValidationError || EdtEndTime.HasValidationError)
                return;
            var args = new ValidationArgs();
            //Use SchedulerFormHelper to get any error in UI part.
            SchedulerFormHelper.ValidateValues(this, args);

            //Any error, app will be show a error message to notify user and focus to error control
            if (!args.Valid)
            {
                DXMessageBox.Show(args.ErrorMessage, Application.ProductName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                FocusInvalidControl(args);
                return;
            }

            //User SchedulerFormHelper to get warning on UI part
            SchedulerFormHelper.CheckForWarnings(this, args);

            //Any warning, app will be ask user to handler or not 
            // If yes, focus to warning control
            if (!args.Valid)
            {
                var answer = DXMessageBox.Show(args.ErrorMessage, Application.ProductName, MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel)
                {
                    FocusInvalidControl(args);
                    return;
                }
            }
            //Check conflict by Appointment controller
            if (!AppointmentVisualController.IsConflictResolved())
            {
                DXMessageBox.Show(SchedulerLocalizer.GetString(SchedulerStringId.Msg_Conflict), Application.ProductName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            if (AppointmentVisualController.ShouldShowRecurrence)
            {
                //if everything is ok, we wil apply recurrence to appoinment.
                RecurrenceVisualController.ApplyRecurrence();
            }

            AppointmentVisualController.ApplyChanges();
        }

        /// <summary>
        /// Cancel appoinment
        /// </summary>
        private void CancelAppointment()
        {
            AppointmentVisualController = new AppointmentFormController(Scheduler, Appointment);
            RecurrenceVisualController = new RecurrenceVisualController(AppointmentVisualController);
            IsEnabled = false;
        }

        /// <summary>
        /// Focus to control
        /// </summary>
        /// <param name="args">validation args</param>
        private void FocusInvalidControl(ValidationArgs args)
        {
            var control = args.Control as UIElement;
            if (control != null)
                control.Focus();
        }

        #endregion
    }
}
