using System;
using System.Collections.ObjectModel;
using AutoMapper;
using DevExpress.XtraScheduler;
using TIG.TotalLink.Client.Module.Admin.Attribute;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler.Interface;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.Facade.Admin;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Core.Scheduler
{
    [SendsDocumentMessage(typeof(SelectedAppointmentChangedMessage))]
    public abstract class SchedulerViewModelBase<TModel> : WidgetViewModelBase, ISchedulerPersistanceProvider
        where TModel : DataObjectBase
    {
        #region Private Fields

        private readonly ObservableCollection<SchedulerResourceitemViewModel> _resources;
        private readonly IAdminFacade _adminFacade;
        private Appointment _selectedItem;
        private ObservableCollection<SchedulerItemViewModel> _itemsSource;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// View model will be monitor Scheduler change event
        /// </summary>
        protected SchedulerViewModelBase(IAdminFacade facade)
        {
            _adminFacade = facade;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Synchronize key for sync between outlook and TotalLink.
        /// </summary>
        public abstract string SynchronizerForeignId { get; }

        /// <summary>
        /// The selected appointment of scheduler view.
        /// </summary>
        public Appointment SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                SetProperty(ref _selectedItem, value, () => SelectedItem, OnSelectedItemChanged);
            }
        }

        /// <summary>
        /// The primary source of items for this list.
        /// </summary>
        public ObservableCollection<SchedulerItemViewModel> ItemsSource
        {
            get { return _itemsSource; }
            set { SetProperty(ref _itemsSource, value, () => ItemsSource); }
        }

        /// <summary>
        /// Resources
        /// </summary>
        public ObservableCollection<SchedulerResourceitemViewModel> Resources
        {
            get { return _resources; }
        }

        /// <summary>
        /// Initialize for Active view 
        /// </summary>
        public virtual SchedulerViewType ActiveViewType { get { return SchedulerViewType.Month; } }

        /// <summary>
        /// Initialize for Goup type
        /// </summary>
        public virtual SchedulerGroupType GroupType { get { return SchedulerGroupType.None; } }

        #endregion


        #region Private Properties

        private void OnSelectedItemChanged()
        {
            // Notify widgets that the selection has changed
            SendDocumentMessage(new SelectedAppointmentChangedMessage(this, SelectedItem));
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Refreshes the ItemsSource.
        /// This will only have an effect if the ItemsSource is a WcfInstantFeedbackDataSourceEx.
        /// </summary>
        protected void Refresh()
        {

        }

        /// <summary>
        /// Save appointment object to data store.
        /// </summary>
        /// <param name="item">Appointment of UI level.</param>
        /// <returns>Updated UI object.</returns>
        public virtual SchedulerItemViewModel Save(SchedulerItemViewModel item)
        {
            TModel appointment = null;
            _adminFacade.ExecuteUnitOfWork(uow =>
            {
                uow.StartUiTracking(this);
                // If Oid is null, we will create a new persistance object. 
                if (item.Oid == default(Guid))
                {
                    appointment = DataObjectHelper.CreateDataObject<TModel>(uow) as TModel;
                }
                // Get object from cache.
                else
                {
                    appointment = uow.GetObjectByKey<TModel>(item.Oid);
                }

                // Map UI object to persistance object.
                Mapper.Map(item, appointment);
            });

            item.Oid = appointment.Oid;
            return item;
        }

        /// <summary>
        /// Delete a appointment by identity key.
        /// </summary>
        /// <param name="key">Indentity key for delete.</param>
        public virtual void Delete(object key)
        {
            _adminFacade.ExecuteUnitOfWork(uow =>
            {
                uow.StartUiTracking(this);

                // Get a copy of the data object in this session
                var sessionDataObject = uow.GetObjectByKey<TModel>(key);
                if (sessionDataObject == null)
                    return;

                // Delete the object
                sessionDataObject.Delete();
            });
        }

        #endregion
    }
}
