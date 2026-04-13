using System.Collections;
using System.Linq;
using System.Windows;
using DevExpress.Utils;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Scheduler;
using DevExpress.Xpf.Scheduler.UI;
using DevExpress.XtraScheduler;
using System.Windows.Data;
using System.Windows.Input;
using AutoMapper;
using DevExpress.Mvvm;
using DevExpress.Xpf.Core;
using DevExpress.XtraScheduler.Outlook;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler.Interface;

namespace TIG.TotalLink.Client.Module.Admin.View.Core.Scheduler
{
    /// <summary>
    /// Interaction logic for SchedulerView.xaml
    /// </summary>
    public partial class SchedulerView
    {
        #region Private Properties

        private string[] _outlookCalendarPaths;
        private ISchedulerPersistanceProvider _persistanceProvider;
        private static AppointmentMapping _appointmentMapping;

        #endregion


        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            "ItemsSource", typeof(IList), typeof(SchedulerView), new FrameworkPropertyMetadata(default(IList), OnItemsSourcePropertyChanged) { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        public static readonly DependencyProperty ResourceSourceProperty = DependencyProperty.Register(
            "ResourceSource", typeof(IList), typeof(SchedulerView), new FrameworkPropertyMetadata(default(IList), OnResourceSourcePropertyChanged) { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

        public static readonly DependencyProperty SelectedAppointmentProperty = DependencyProperty.Register(
            "SelectedAppointment", typeof(Appointment), typeof(SchedulerView), new FrameworkPropertyMetadata(default(Appointment)) { BindsTwoWayByDefault = true, DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });


        /// <summary>
        /// Selected Appointment
        /// </summary>
        public Appointment SelectedAppointment
        {
            get { return (Appointment)GetValue(SelectedAppointmentProperty); }
            set { SetValue(SelectedAppointmentProperty, value); }
        }

        /// <summary>
        /// Resources
        /// </summary>
        public IList ResourceSource
        {
            get { return (IList)GetValue(ResourceSourceProperty); }
            set { SetValue(ResourceSourceProperty, value); }
        }

        /// <summary>
        /// Items Source
        /// </summary>
        public IList ItemsSource
        {
            get { return (IList)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }

        /// <summary>
        /// Items Source change method
        /// 1. We will give items source to real scheduler control
        /// 2. According to Resource to set mapping
        /// </summary>
        /// <param name="obj">Scheduler view</param>
        /// <param name="e">Message for items change</param>
        private static void OnItemsSourcePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var schedulerView = (SchedulerView)obj;
            SetAppointmentResourceMapping(schedulerView);
            schedulerView.Scheduler.Storage.AppointmentStorage.DataSource = e.NewValue;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Save Command
        /// </summary>
        public ICommand SyncAppointmentCommand
        {
            get { return new DelegateCommand(SyncAppointToOutlook); }
        }

        /// <summary>
        /// Outlook Calendar Paths
        /// </summary>
        public string[] OutlookCalendarPaths
        {
            get
            {
                if (_outlookCalendarPaths != null)
                    return _outlookCalendarPaths;

                try
                {
                    _outlookCalendarPaths = OutlookExchangeHelper.GetOutlookCalendarPaths();
                }
                catch
                {
                    _outlookCalendarPaths = new string[0];
                }
                return _outlookCalendarPaths;
            }
        }

        #endregion


        #region Property Changed Methods

        /// <summary>
        /// According to any resources to give appointment mapping to scheduler control
        /// </summary>
        /// <param name="schedulerView">Scheduler view</param>
        private static void SetAppointmentResourceMapping(SchedulerView schedulerView)
        {
            var resourceKey = schedulerView.ResourceSource != null && schedulerView.ResourceSource.Count > 0
                ? "AppointmentMappingWithResource"
                : "AppointmentMapping";
            _appointmentMapping = schedulerView.TryFindResource(resourceKey) as AppointmentMapping;
            if (_appointmentMapping != null)
            {
                schedulerView.Scheduler.Storage.AppointmentStorage.Mappings = _appointmentMapping;
            }
        }

        /// <summary>
        /// Resource source change method
        /// </summary>
        /// <param name="obj">Scheduler View</param>
        /// <param name="e">Message for Resource change</param>
        private static void OnResourceSourcePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var schedulerView = (SchedulerView)obj;
            SetAppointmentResourceMapping(schedulerView);
            var resourceMapping = schedulerView.TryFindResource("ResourceMapping") as ResourceMapping;
            if (resourceMapping != null)
            {
                schedulerView.Scheduler.Storage.ResourceStorage.Mappings = resourceMapping;
            }
            schedulerView.Scheduler.Storage.ResourceStorage.DataSource = e.NewValue;
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public SchedulerView()
        {
            InitializeComponent();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sync appoints to outlook client
        /// </summary>
        private void SyncAppointToOutlook()
        {
            if (_persistanceProvider == null)
            {
                return;
            }

            // Sync appointment to all outlook calendar paths.
            foreach (var outlookCalendarPath in OutlookCalendarPaths)
            {
                // Get outlook exporter from current scheduler.
                var synchronizer = new OutlookExportSynchronizer(Scheduler.Storage.GetCoreStorage());
                ((ISupportCalendarFolders)synchronizer).CalendarFolderName = outlookCalendarPath;

                // Get syncronizer foreign key from persistance provider.
                synchronizer.ForeignIdFieldName = _persistanceProvider.SynchronizerForeignId;
                // Booking syncing event.
                synchronizer.AppointmentSynchronizing += Synchronizer_AppointmentSynchronizing;

                // Report to progress bar.
                BeforeSynchronization(synchronizer.SourceObjectCount);
                try
                {
                    synchronizer.Synchronize();
                }
                finally
                {
                    AfterSynchronization();
                }
            }
        }

        /// <summary>
        /// Before sync appointments
        /// </summary>
        /// <param name="objectCount">Count of appointments</param>
        private void BeforeSynchronization(int objectCount)
        {
            progressBar.Value = 0;
            progressBar.Maximum = objectCount;
        }

        /// <summary>
        /// After synced appointments
        /// </summary>
        private void AfterSynchronization()
        {
            progressBar.Value = 0;
        }

        #endregion


        #region Event handlers

        /// <summary>
        /// Event handler for synchronizing appointment.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Synchronizer_AppointmentSynchronizing(object sender, AppointmentSynchronizingEventArgs e)
        {
            progressBar.Value += 1;
            DispatcherHelper.DoEvents();
        }

        /// <summary>
        /// Scheduler Loaded
        /// </summary>
        /// <param name="sender">scheduler view</param>
        /// <param name="e">Router event args</param>
        private void SchedulerView_OnLoaded(object sender, RoutedEventArgs e)
        {
            // 1. Register appointment change events
            #region Appointment change events

            Scheduler.Storage.AppointmentsInserted -= Storage_AppointmentsInserted;
            Scheduler.Storage.AppointmentsInserted += Storage_AppointmentsInserted;

            Scheduler.Storage.AppointmentsChanged -= Storage_AppointmentsChanged;
            Scheduler.Storage.AppointmentsChanged += Storage_AppointmentsChanged;

            Scheduler.Storage.AppointmentDeleting -= Storage_AppointmentDeleting;
            Scheduler.Storage.AppointmentDeleting += Storage_AppointmentDeleting;

            #endregion

            // 2. Register selected collection changed event
            Scheduler.SelectedAppointments.CollectionChanged += SelectedAppointments_CollectionChanged;

            // 3. Get Persisitance provider
            _persistanceProvider = DataContext as ISchedulerPersistanceProvider;
        }

        /// <summary>
        /// Selected Appointment change event
        /// </summary>
        /// <param name="sender">Selected appointments collection</param>
        /// <param name="e">Message for collection changed</param>
        private void SelectedAppointments_CollectionChanged(object sender, CollectionChangedEventArgs<Appointment> e)
        {
            //Because native appointments can be muilt selected in scheduler control, but in our system just want single select and show it to detail view.
            //So only select an appointment will be trigger select appointment property, otherwise it will be set to null and detail view will be automatic disable. 
            SelectedAppointment = Scheduler.SelectedAppointments.Count == 1 ? Scheduler.SelectedAppointments[0] : null;
        }

        /// <summary>
        /// Appointment deleting method
        /// </summary>
        /// <param name="sender">Who send this message</param>
        /// <param name="e">Message for which one be deleted</param>
        private void Storage_AppointmentDeleting(object sender, PersistentObjectCancelEventArgs e)
        {
            // Get inner appointment object.
            var appointment = e.Object as Appointment;
            if (appointment == null
                || appointment.Id == null)
                return;

            if (_persistanceProvider == null)
            {
                return;
            }

            // Using persistance provider to delete this appointment in data store.
            _persistanceProvider.Delete(appointment.Id);
        }

        /// <summary>
        /// Appointment update method
        /// </summary>
        /// <param name="sender">Who send this message</param>
        /// <param name="e">Message for which one be updated</param>
        void Storage_AppointmentsChanged(object sender, PersistentObjectsEventArgs e)
        {
            if (_persistanceProvider == null)
            {
                return;
            }

            foreach (var item in e.Objects.Cast<Appointment>()
                .Select(
                    appointment =>
                        appointment.GetSourceObject(Scheduler.Storage.GetCoreStorage()) as SchedulerItemViewModel)
                .Where(item => item != null))
            {
                _persistanceProvider.Save(item);
            }
        }

        /// <summary>
        /// Event handler for inner save appointment successed.
        /// </summary>
        /// <param name="sender">Who send this message</param>
        /// <param name="e">Message for which one be Inserted</param>
        private void Storage_AppointmentsInserted(object sender, PersistentObjectsEventArgs e)
        {
            // Check provider
            if (_persistanceProvider == null)
            {
                return;
            }

            foreach (var appointment in e.Objects.Cast<Appointment>())
            {
                var dataObject = appointment.GetSourceObject(Scheduler.Storage.GetCoreStorage()) as SchedulerItemViewModel ??
                                 Mapper.Map<Appointment, SchedulerItemViewModel>(appointment);

                _persistanceProvider.Save(dataObject);

                Scheduler.Storage.SetAppointmentId(appointment, dataObject.Oid);
                Scheduler.Storage.RefreshData();
            }
        }

        /// <summary>
        /// Edit appointment Form Showing event handler
        /// In our system, we will show selected appointment detail on right panel, so we just cancel default behaviour
        /// </summary>
        /// <param name="sender">Scheduler view</param>
        /// <param name="e">Message for appointment form showing</param>
        private void Scheduler_OnEditAppointmentFormShowing(object sender, EditAppointmentFormEventArgs e)
        {
            //Set appointment when edit serise baritem be clicked.
            SelectedAppointment = e.Appointment;
            e.Cancel = true;
        }

        /// <summary>
        /// Sheduler popup menu showing event handler
        /// </summary>
        /// <param name="sender">Scheduler Control</param>
        /// <param name="e">Scheduler popup menu showing message</param>
        private void Scheduler_OnPopupMenuShowing(object sender, SchedulerMenuEventArgs e)
        {
            //Make sure showing up menu for appointment
            if (e.Menu.Name != SchedulerMenuItemName.AppointmentMenu) return;
            var barItemsLinks = e.Menu.ItemLinks.OfType<BarItemLink>().ToList();
            //Find open bar item, if it exist, delete it.
            var openLink =
                barItemsLinks.FirstOrDefault(item => item.BarItemName == SchedulerMenuItemName.OpenAppointment);
            if (openLink != null)
            {
                e.Menu.ItemLinks.Remove(openLink);
            }

            //Find edit series bar item, if it exist, delete it.
            var editSeries =
                barItemsLinks.FirstOrDefault(item => item.BarItemName == SchedulerMenuItemName.EditSeries);
            if (editSeries != null)
            {
                e.Menu.ItemLinks.Remove(editSeries);
            }
        }

        #endregion
    }
}
