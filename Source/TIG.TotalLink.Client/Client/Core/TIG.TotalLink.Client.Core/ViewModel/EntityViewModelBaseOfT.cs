using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using AutoMapper;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Core.ViewModel
{
    /// <summary>
    /// Generic base classs for viewmodels which wrap a DataObjectBase.
    /// </summary>
    public abstract class EntityViewModelBase<TDataObject> : EntityViewModelBase
        where TDataObject : DataObjectBase
    {
        #region Private Structs

        private struct SyncFromDataObjectPropertyInfo
        {
            public PropertyInfo ViewModelProperty;
            public PropertyInfo DataObjectProperty;
        }

        #endregion


        #region Private Fields

        private readonly List<SyncFromDataObjectPropertyInfo> _syncFromDataObjectProperties;
        private readonly List<XPCollectionChangedEventHandler> _syncFromDataObjectHandlers = new List<XPCollectionChangedEventHandler>();

        #endregion


        #region Constructors

        protected EntityViewModelBase()
        {
            // Get all properties with an AssignParentViewModelAttribute and handle the CollectionChanged event on the property value 
            var assignParentViewModelProperties = GetType().GetProperties().Where(p => p.IsDefined(typeof(AssignParentViewModelAttribute), true));
            foreach (var property in assignParentViewModelProperties)
            {
                var collection = (INotifyCollectionChanged)property.GetValue(this);
                collection.CollectionChanged += AssignParentViewModel_CollectionChanged;
            }

            // Store all properties with a SyncFromDataModelAttribute so the CollectionChanged events can be handled later
            _syncFromDataObjectProperties = GetType().GetProperties()
                .Where(p => p.IsDefined(typeof(SyncFromDataObjectAttribute), true))
                .Select(p => new SyncFromDataObjectPropertyInfo
                {
                    ViewModelProperty = p,
                    DataObjectProperty = typeof(TDataObject).GetProperty(p.Name, BindingFlags.Instance | BindingFlags.Public)
                })
                .ToList();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The contained data object.
        /// </summary>
        public TDataObject DataObject
        {
            get { return (TDataObject)_dataObject; }
            protected set
            {
                var oldDataModel = DataObject;
                SetProperty(ref _dataObject, value, () => DataObject, () => UpdateDataObjectEvents(oldDataModel, DataObject));
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates events on the data object. 
        /// </summary>
        /// <param name="oldDataObject">The old data object to remove events from.</param>
        /// <param name="newDataObject">The new data object to add events to.</param>
        private void UpdateDataObjectEvents(TDataObject oldDataObject, TDataObject newDataObject)
        {
            // Remove events from the old data object
            if (oldDataObject != null)
            {
                // REmove the property changed handler
                oldDataObject.Changed -= DataObject_PropertyChanged;

                // Remove CollectionChanged events for the SyncFromDataObjectAttribute
                foreach (var property in _syncFromDataObjectProperties)
                {
                    // Get the datamodel collection
                    var collection = property.DataObjectProperty.GetValue(oldDataObject) as XPBaseCollection;
                    if (collection == null)
                        continue;

                    // Attempt to find a CollectionChanged handler for the collection
                    var handler = _syncFromDataObjectHandlers.SingleOrDefault(h => ReferenceEquals(h.Target, collection));
                    if (handler == null)
                        continue;

                    // Stop handling the event
                    collection.CollectionChanged -= handler;
                    _syncFromDataObjectHandlers.Remove(handler);
                }
            }

            // Add events on the new data object
            if (newDataObject != null)
            {
                // Add a property changed handler
                newDataObject.Changed += DataObject_PropertyChanged;

                // Add CollectionChanged events for the SyncFromDataObjectAttribute
                foreach (var property in _syncFromDataObjectProperties)
                {
                    // Abort if the viewmodel collection is not a generic type
                    if (!property.ViewModelProperty.PropertyType.IsGenericType)
                        continue;

                    // Get the data object collection
                    var collection = property.DataObjectProperty.GetValue(newDataObject) as XPBaseCollection;
                    if (collection == null)
                        continue;

                    // Create and store a CollectionChanged handler
                    var property1 = property;
                    XPCollectionChangedEventHandler handler = (s, e) => SyncFromDataObject_CollectionChanged(s, e, property1);
                    _syncFromDataObjectHandlers.Add(handler);

                    // Handle the event
                    collection.CollectionChanged += handler;
                } 
            }
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when a property changes on the data object.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDataObjectPropertyChanged(ObjectChangeEventArgs e)
        {
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the Changed event on the data object.
        /// </summary>
        /// <param name="sender">Object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void DataObject_PropertyChanged(object sender, ObjectChangeEventArgs e)
        {
            OnDataObjectPropertyChanged(e);
        }

        /// <summary>
        /// Handles the CollectionChanged event on viewmodel collections with an AssignParentViewModelAttribute.
        /// </summary>
        /// <param name="sender">Object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void AssignParentViewModel_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // When a viewmodel is added, assign this viewmodel as the parent
                    foreach (var viewModel in e.NewItems.OfType<ISupportParentViewModel>())
                    {
                        viewModel.ParentViewModel = this;
                    }
                    break;

               case NotifyCollectionChangedAction.Remove:
                    // When a viewmodel is removed, clear the parent
                    foreach (var viewModel in e.OldItems.OfType<ISupportParentViewModel>())
                    {
                        viewModel.ParentViewModel = null;
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles the CollectionChanged event on datamodel collections where the associated viewmodel property has a SyncFromDataObjectAttribute.
        /// </summary>
        /// <param name="sender">Object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// <param name="property">Information about the properties that are involved in the sync.</param>
        private void SyncFromDataObject_CollectionChanged(object sender, XPCollectionChangedEventArgs e, SyncFromDataObjectPropertyInfo property)
        {
            // Get the type of object that the viewmodel collection contains
            var viewModelType = property.ViewModelProperty.PropertyType.GenericTypeArguments[0];

            // Get the viewmodel collection
            var viewModelCollection = property.ViewModelProperty.GetValue(this) as IList;
            if (viewModelCollection == null)
                return;

            switch (e.CollectionChangedType)
            {
                case XPCollectionChangedType.AfterAdd:
                    // An item has been added to the datamodel collection
                    // So we need to map it to a corresponding viewmodel and add the result to the viewmodel collection
                    viewModelCollection.Add(Mapper.Map(e.ChangedObject, e.ChangedObject.GetType(), viewModelType));
                    break;

                case XPCollectionChangedType.BeforeRemove:
                    // An item has been removed from the datamodel collection
                    // So we need to find the corresponding viewmodel in the viewmodel collection and remove it
                    var viewModel = viewModelCollection.Cast<EntityViewModelBase>().SingleOrDefault(v => ReferenceEquals(v.DataObjectAsBase, e.ChangedObject));
                    if (viewModel != null)
                        viewModelCollection.Remove(viewModel);
                    break;
            }
        }

        #endregion
    }
}
