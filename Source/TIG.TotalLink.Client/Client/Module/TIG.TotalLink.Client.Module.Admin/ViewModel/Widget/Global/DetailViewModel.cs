using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DevExpress.Xpf.Ribbon.Customization;
using DevExpress.Xpo;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Editor.Extension;
using TIG.TotalLink.Client.Module.Admin.Enum.KeyedData;
using TIG.TotalLink.Client.Module.Admin.Helper;
using TIG.TotalLink.Client.Module.Admin.Interface;
using TIG.TotalLink.Client.Module.Admin.Message;
using TIG.TotalLink.Client.Module.Admin.ViewModel.Core;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TypeExtension = TIG.TotalLink.Client.Core.Extension.TypeExtension;

namespace TIG.TotalLink.Client.Module.Admin.ViewModel.Widget.Global
{
    public class DetailViewModel : WidgetViewModelBase, ISupportLayoutData, ISupportModelIinitialization
    {
        #region Public Events

        public event EventHandler<SelectedItemsChangingEventArgs> SelectedItemsChanging;

        #endregion


        #region Static Fields

        private static readonly Dictionary<string, byte[]> DefaultLayouts = new Dictionary<string, byte[]>();

        #endregion


        #region Private Fields

        private UnitOfWork _editUnitOfWork;
        private UnitOfWork _cloneUnitOfWork;
        private INotifyPropertyChanged _currentItem;
        private ChildModelInfo _childInfo;
        private string _currentItemDescription;
        private readonly Dictionary<string, NotifyCollectionChangedEventHandler> _collectionChangedHandlers = new Dictionary<string, NotifyCollectionChangedEventHandler>();

        #endregion


        #region Constructors

        public DetailViewModel()
        {
            // Initialize collections
            CurrentItems = new ObservableCollection<INotifyPropertyChanged>();

            // Initialize messages
            DefaultMessenger.Register<EntityChangedMessage>(this, OnEntityChangedMessage);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// All of the items currently being edited.
        /// </summary>
        public ObservableCollection<INotifyPropertyChanged> CurrentItems { get; private set; }

        /// <summary>
        /// The item currently being edited.
        /// This item represents a combination of all the items in the CurrentItems collection.
        /// This will be null if all items in the CurrentItems collection are not the same type.
        /// </summary>
        public INotifyPropertyChanged CurrentItem
        {
            get { return _currentItem; }
            set
            {
                if (!Equals(_currentItem, value))
                    OnCurrentItemChanging(_currentItem, value);

                var oldItem = _currentItem;
                SetProperty(ref _currentItem, value, () => CurrentItem, () => OnCurrentItemChanged(oldItem, value));
            }
        }

        /// <summary>
        /// Contains information about the parent and child items, when the CurrentItem is a child item found via ModelInitializers.
        /// </summary>
        public ChildModelInfo ChildInfo
        {
            get { return _childInfo; }
            set
            {
                if (!Equals(_childInfo, value))
                    OnChildInfoChanging(_childInfo, value);

                var oldItem = _childInfo;
                SetProperty(ref _childInfo, value, () => ChildInfo, () => OnChildInfoChanged(oldItem, value));
            }
        }

        /// <summary>
        /// Indicates if any items are currently being edited.
        /// This will be false if all items in the CurrentItems collection are not the same type.
        /// </summary>
        public virtual bool HasCurrentItem
        {
            get { return (CurrentItem != null); }
        }

        /// <summary>
        /// A description of the current items.
        /// </summary>
        public string CurrentItemDescription
        {
            get { return _currentItemDescription; }
            private set { SetProperty(ref _currentItemDescription, value, () => CurrentItemDescription); }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates one property on all CurrentItems with the value from CurrentItem.
        /// </summary>
        /// <param name="propertyName">The name of the property to write.</param>
        private async Task WritePropertyAsync(string propertyName)
        {
            // Abort if there is no active edit and the CurrentItem is DataObjectBase
            // In this case, the CurrentItem will be the same instance of the object in CurrentItems, so there is no need to copy values
            if (_editUnitOfWork == null && CurrentItem is DataObjectBase)
                return;

            // Get the property being modified and abort if it is read-only
            var property = CurrentItem.GetType().GetProperty(propertyName);
            if (!property.CanWrite)
                return;

            // Get the new value for the property
            var newValue = property.GetValue(CurrentItem);

            // If the CurrentItems contains DataObjectBase, use a facade to modify them
            var dataObjects = CurrentItems.OfType<DataObjectBase>().ToList();
            if (dataObjects.Any())
            {
                // Update the property value on each data object
                foreach (var dataObject in dataObjects)
                {
                    dataObject.SetDataProperty(propertyName, newValue, false, false);
                }

                // Commit the changes
                await _editUnitOfWork.CommitChangesAsync();
            }
            else // If the CurrentItems does not contain DataObjectBase, modify them directly
            {
                // Update the property value on each object in CurrentItems
                foreach (var item in CurrentItems)
                {
                    property.SetValue(item, newValue);
                }
            }
        }

        /// <summary>
        /// Syncs contents of a collection property on all CurrentItems with the collection contents from CurrentItem.
        /// </summary>
        /// <param name="property">The collection property whose contents have changed.</param>
        /// <param name="e">Event arguments describing the changes that occurred.</param>
        /// <param name="sourceList">The list which triggered the collection changed event.</param>
        private void SyncCollectionProperty(PropertyInfo property, NotifyCollectionChangedEventArgs e, INotifyCollectionChanged sourceList)
        {
            // Update the collection contents on each object in CurrentItems
            foreach (var item in CurrentItems)
            {
                // Attempt to get the destList and abort if it is null or equal to the sourceList
                var destList = property.GetValue(item) as IList;
                if (destList == null || ReferenceEquals(destList, sourceList))
                    continue;
                
                // Sync the changes to the destList
                destList.SyncChanges(e);
            }
        }

        /// <summary>
        /// Clears the CurrentItems collection.
        /// </summary>
        private void ClearCurrentItems()
        {
            // Remove PropertyChanged handlers from all items that are not DataObjectBase
            foreach (var item in CurrentItems.Where(i => !(i is DataObjectBase)))
            {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // Clear the CurrentItems collection and ChildInfo
            CurrentItems.Clear();
            ChildInfo = null;

            // Dispose of both UnitOfWorks
            if (_editUnitOfWork != null)
            {
                if (_editUnitOfWork.IsObjectsSaving || _editUnitOfWork.GetObjectsToSave().Count > 0 || _editUnitOfWork.GetObjectsToDelete().Count > 0)
                {
                    // If the editUnitOfWork still has changes, flag it to dispose after the changes are saved
                    // otherwise we might dispose it while WriteChangesAsync is still executing
                    _editUnitOfWork.DisposeAfterCommit();
                }
                else
                {
                    try
                    {
                        _editUnitOfWork.Dispose();
                    }
                    catch
                    {
                        // Ignore dispose errors
                    }
                }
            }
            _editUnitOfWork = null;

            if (_cloneUnitOfWork != null)
            {
                try
                {
                    _cloneUnitOfWork.Dispose();
                }
                catch
                {
                    // Ignore dispose errors
                }
            }
            _cloneUnitOfWork = null;
        }

        /// <summary>
        /// Updates the CurrentItems with the supplied items.
        /// </summary>
        /// <param name="selectedItems">The items to replace the CurrentItems collection with.</param>
        private void SetCurrentItems(IList<INotifyPropertyChanged> selectedItems)
        {
            // Clear the CurrentItems collection
            ClearCurrentItems();

            // If the selection contains DataObjectBase, we need to create UnitsOfWork to track changes
            var firstSelectedDataObject = selectedItems.OfType<DataObjectBase>().FirstOrDefault();
            if (firstSelectedDataObject != null)
            {
                // Get a facade to edit the items
                var facade = firstSelectedDataObject.GetFacade();
                if (facade == null)
                    throw new Exception(string.Format("Failed to find facade to edit a {0}", firstSelectedDataObject.GetType().Name));

                // Create a UnitOfWork to write changes to the CurrentItems collection, and another to hold the CurrentItem clone
                _editUnitOfWork = facade.CreateUnitOfWork();
                if (_editUnitOfWork != null)
                    _editUnitOfWork.StartUiTracking(this, true, true, true);
                _cloneUnitOfWork = facade.CreateUnitOfWork();

                // Add session copies of all data objects from the selection to the CurrentItems collection
                var dataObjects = selectedItems.OfType<DataObjectBase>().ToList();
                CurrentItems.AddRange(dataObjects.Select(o =>
                {
                    // If the data object exists in the local session, we won't be able to get a session copy, so just return the original item
                    if (o.IsLocalOnly)
                        return o;

                    // Attempt to get a session copy of the data object
                    return _editUnitOfWork.GetDataObject(o, o.GetType()) ?? o;
                }));
            }
            else // If the selection does not contain DataObjectBase, we don't need to write changes so we can just work with the selected items directly
            {
                // Add objects from the selection to the CurrentItems collection
                foreach (var item in selectedItems.Where(i => i != null))
                {
                    CurrentItems.Add(item);

                    // We won't receive EntityChangedMessages when non-dataobjects are modified, so we need to handle the PropertyChanged event
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }
        }

        /// <summary>
        /// Updates the CurrentItem based on the contents of CurrentItems.
        /// </summary>
        private void UpdateCurrentItem()
        {
            // If no items are selected, or the selected items are not all the same type, clear the CurrentItem and abort
            if (!CurrentItems.AreSameType())
            {
                CurrentItem = null;
                CurrentItemDescription = null;
                return;
            }

            // If the CurrentItems contains DataObjectBase, create the CurrentItem in a UnitOfWork
            var dataObjects = CurrentItems.OfType<DataObjectBase>().ToList();
            INotifyPropertyChanged currentItem = null;
            IList currentItems;
            if (dataObjects.Any())
            {
                currentItems = dataObjects;

                // Create the CurrentItem as a clone of the first data object, and initialize with values from all selected data objects
                currentItem = dataObjects.First().Clone(_cloneUnitOfWork, false);
                currentItem.CopyFrom(dataObjects, false, false);
            }
            else // If the CurrentItems does not contain DataObjectBase, create the CurrentItem directly
            {
                currentItems = CurrentItems;
                var currentItemType = CurrentItems.First().GetType();

                // Attempt to resolve an instance of the currentItemType via Autofac
                try
                {
                    currentItem = (INotifyPropertyChanged)AutofacViewLocator.Default.Resolve(currentItemType);
                }
                catch (Exception)
                {
                    // Ignore Resolve errors
                }

                // If Autofac did not resolve the currentItemType, create one via reflection
                if (currentItem == null)
                    currentItem = (INotifyPropertyChanged)Activator.CreateInstance(currentItemType);

                // Initialize the currentItem with values from all selected objects
                currentItem.CopyFrom(CurrentItems, false, false);
            }

            // Attempt to find a child model
            ChildModelInfo childInfo;
            if (GetChildModelRecursive(currentItem, out childInfo))
            {
                // If a child model was found, assign it as the CurrentItem
                CurrentItem = childInfo.ChildItem;
                CurrentItemDescription = ActionMessageHelper.GetDescription(childInfo.ChildItem);
            }
            else
            {
                // If a child model was not found, assign the original item as the CurrentItem
                CurrentItem = currentItem;
                CurrentItemDescription = ActionMessageHelper.GetDescription(currentItems);
            }

            // Update the ChildInfo
            ChildInfo = childInfo;
        }

        /// <summary>
        /// Attempts to find a child model for the supplied item based on the ModelInitializers that are defined.
        /// This will recursively iterate through child properties until an object is found that has no ModelInitializer.
        /// </summary>
        /// <param name="currentItem">The item to attempt to find a child model for.</param>
        /// <param name="childInfo">Information about the child model that was found.</param>
        /// <returns>True if a child model was found; otherwise false.</returns>
        private bool GetChildModelRecursive(INotifyPropertyChanged currentItem, out ChildModelInfo childInfo)
        {
            childInfo = null;

            // Abort if the currentItem is null
            if (currentItem == null)
                return false;

            // Attempt to get a WidgetModelInit for the currentItem type
            var currentItemType = currentItem.GetType();
            var modelInit = ModelInitData.ModelInitializers.FirstOrDefault(i => i.InitMode == WidgetModelInit.InitModes.DisplayChild && !i.IsEmpty && i.DataModelType == currentItemType);
            if (modelInit == null)
                return false;

            // Attempt to get the childProperty
            var childProperty = currentItemType.GetProperty(modelInit.ChildPropertyName);
            if (childProperty == null)
                return false;

            // If CurrentItems only contain one item, then use that item directly
            // Otherwise we have to use the currentItem (clone)
            // This means that the Detail widget can only watch for childProperty changes on the parentItem when only one item is selected
            var parentItem = CurrentItems.Count == 1 ? CurrentItems[0] : currentItem;

            // Get the childItem from the childProperty on the parentItem
            var childItem = childProperty.GetValue(parentItem) as INotifyPropertyChanged;

            // Continue collecting children recursively until no child is found
            bool childFound;
            do
            {
                ChildModelInfo nextChildInfo;
                childFound = GetChildModelRecursive(childItem, out nextChildInfo);
                if (childFound)
                {
                    parentItem = nextChildInfo.ParentItem;
                    childItem = nextChildInfo.ChildItem;
                    childProperty = nextChildInfo.ChildProperty;
                }
            } while (childFound);

            // Replace the CurrentItems collection with a new list containing the last found child
            SetCurrentItems(new List<INotifyPropertyChanged> { childItem });
            childItem = CurrentItems.FirstOrDefault();

            // Create a clone of the childItem to return
            // (unless the childItem exists in the default session, or it is null)
            var childDataObject = childItem as DataObjectBase;
            if (childDataObject != null)
            {
                if (!childDataObject.IsLocalOnly)
                    childItem = childDataObject.Clone(_cloneUnitOfWork, true, false);
            }
            else
            {
                if (childItem != null)
                    childItem = (INotifyPropertyChanged)Activator.CreateInstance(childItem.GetType());
            }

            // Update the childInfo and return true to indicate that a child was found
            childInfo = new ChildModelInfo()
            {
                ParentItem = parentItem,
                ChildItem = childItem,
                ChildProperty = childProperty
            };
            return true;
        }

        /// <summary>
        /// Saves the layout for the supplied item, but only if the layout has changed from the last saved or default.
        /// </summary>
        /// <param name="item">The item to save the layout for.</param>
        private void SaveItemLayout(INotifyPropertyChanged item)
        {
            // Abort if the item is null
            if (item == null)
                return;

            // Get the item type name
            var itemTypeName = item.GetType().FullName;

            // Get the current layout
            var newLayout = GetLayout();

            // Attempt to get the saved layout
            var savedLayout = GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.DataFormLayout, itemTypeName);

            // If there is a saved layout for this item type, and it's different from the new layout, then save the layout
            if (savedLayout != null)
            {
                if (!((MemoryStream)newLayout).ToArray().SequenceEqual(savedLayout.ToArray()))
                    SaveItemLayout(itemTypeName, newLayout);

                // If there was a saved layout, we won't continue to conmpare with the default layout
                return;
            }

            // Attempt to get the default layout
            byte[] defaultLayout;
            DefaultLayouts.TryGetValue(itemTypeName, out defaultLayout);

            // If there is no default layout for this item type, or the new layout is different from the default layout, then save the layout
            if (defaultLayout == null || !((MemoryStream)newLayout).ToArray().SequenceEqual(defaultLayout))
                SaveItemLayout(itemTypeName, newLayout);
        }

        /// <summary>
        /// Saves the supplied layout for the specified entity type.
        /// </summary>
        /// <param name="itemTypeName">The full type name of the entity that the layout belongs to.</param>
        /// <param name="layout">The layout to save.</param>
        private void SaveItemLayout(string itemTypeName, Stream layout)
        {
            SetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.DataFormLayout, itemTypeName, layout);
        }

        /// <summary>
        /// Raises the SelectedItemsChanging event.
        /// </summary>
        /// <returns>True if the event was handled; otherwise false.</returns>
        private bool RaiseSelectedItemsChanging(SelectedItemsChangedMessage message)
        {
            var e = new SelectedItemsChangingEventArgs(message);
            OnSelectedItemsChanging(e);
            return e.Handled;
        }

        #endregion


        #region Protected Methods

        protected virtual void OnCurrentItemChanging(INotifyPropertyChanged oldItem, INotifyPropertyChanged newItem)
        {
            if (oldItem != null)
            {
                // Remove event handlers from the old item
                oldItem.PropertyChanged -= CurrentItem_PropertyChanged;

                // Remove CollectionChanged events from all properties on CurrentItem which are an ObservableCollection
                foreach (var collectionProperty in oldItem.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => TypeExtension.IsAssignableFromGeneric(typeof(ObservableCollection<>), p.PropertyType)))
                {
                    NotifyCollectionChangedEventHandler collectionChangedHandler;
                    if (!_collectionChangedHandlers.TryGetValue(collectionProperty.Name, out collectionChangedHandler))
                        continue;

                    _collectionChangedHandlers.Remove(collectionProperty.Name);

                    var collection = collectionProperty.GetValue(oldItem) as INotifyCollectionChanged;
                    if (collection != null)
                        collection.CollectionChanged -= collectionChangedHandler;
                }

                // If the oldItem supports storing a parent view model, clear it
                var supportParentViewModel = oldItem as ISupportParentViewModel;
                if (supportParentViewModel != null)
                    supportParentViewModel.ParentViewModel = null;
            }

            if (newItem != null)
            {
                // If the newItem supports storing a parent view model, assign this as the parent so the newItem can access Mvvm Services
                var supportParentViewModel = newItem as ISupportParentViewModel;
                if (supportParentViewModel != null)
                    supportParentViewModel.ParentViewModel = this;

                // Attach event handlers to the new item
                newItem.PropertyChanged += CurrentItem_PropertyChanged;

                // Add CollectionChanged events for all properties on CurrentItem which are an ObservableCollection
                foreach (var collectionProperty in newItem.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => TypeExtension.IsAssignableFromGeneric(typeof(ObservableCollection<>), p.PropertyType)))
                {
                    var collection = collectionProperty.GetValue(newItem) as INotifyCollectionChanged;
                    if (collection != null)
                    {
                        var property = collectionProperty;
                        var collectionChangedHandler = new NotifyCollectionChangedEventHandler((s, e) => CurrentItem_CollectionChanged(s, e, property));
                        _collectionChangedHandlers.Add(property.Name, collectionChangedHandler);
                        collection.CollectionChanged += collectionChangedHandler;
                    }
                }
            }

            // Store the layout for the oldItem
            SaveItemLayout(oldItem);
        }

        protected virtual void OnCurrentItemChanged(INotifyPropertyChanged oldItem, INotifyPropertyChanged newItem)
        {
            // Notify that HasCurrentItem may have changed
            RaisePropertyChanged(() => HasCurrentItem);

            if (newItem != null)
            {
                var itemTypeName = newItem.GetType().FullName;

                // Store the default layout for the newItem
                if (!DefaultLayouts.ContainsKey(itemTypeName))
                    DefaultLayouts.Add(itemTypeName, ((MemoryStream)GetLayout()).ToArray());

                // Restore the layout for the newItem
                IgnoreDocumentModifications = true;
                SetLayout(GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.DataFormLayout, itemTypeName));
                IgnoreDocumentModifications = false;
            }
        }

        protected virtual void OnChildInfoChanging(ChildModelInfo oldItem, ChildModelInfo newItem)
        {
            if (oldItem != null)
            {
                // Remove event handlers from the old item
                oldItem.ParentItem.PropertyChanged -= ParentItem_PropertyChanged;
            }

            if (newItem != null)
            {
                // Attach event handlers to the new item
                newItem.ParentItem.PropertyChanged += ParentItem_PropertyChanged;
            }
        }

        protected virtual void OnChildInfoChanged(ChildModelInfo oldItem, ChildModelInfo newItem)
        {
        }

        /// <summary>
        /// Called just before the selected items are changed by a SelectedItemsChangedMessage.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnSelectedItemsChanging(SelectedItemsChangingEventArgs e)
        {
            if (SelectedItemsChanging != null)
                SelectedItemsChanging(this, e);
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called when the selected items have changed in a related widget.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnSelectedItemsChangedMessage(SelectedItemsChangedMessage message)
        {
            // Raise a SelectedItemsChanging event and abort if the message was handled elsewhere
            if (RaiseSelectedItemsChanging(message))
                return;

            // Set the CurrentItems with all selected items that implement INotifyPropertyChanged
            SetCurrentItems(message.SelectedItems.OfType<INotifyPropertyChanged>().ToList());

            // Update the CurrentItem from the CurrentItems
            UpdateCurrentItem();
        }

        /// <summary>
        /// Handler for the InitializeDocumentMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnInitializeDocumentMessage(InitializeDocumentMessage message)
        {
            // Set the CurrentItems with a new list containing the parameter as an INotifyPropertyChanged
            SetCurrentItems(new List<INotifyPropertyChanged> { message.Parameter as INotifyPropertyChanged });

            // Update the CurrentItem from the CurrentItems
            UpdateCurrentItem();
        }

        /// <summary>
        /// Handler for the EntityChangedMessage.
        /// </summary>
        /// <param name="message">The message that triggered this method.</param>
        private void OnEntityChangedMessage(EntityChangedMessage message)
        {
            // Ignore the message if it came from within this widget
            if (ReferenceEquals(message.Sender, this))
                return;

            // Get a list of changes that contain data objects that are also in CurrentItems
            var changes = message.GetChangesWithOids(CurrentItems.OfType<DataObjectBase>().Select(o => o.Oid).ToArray()).ToList();
            if (changes.Count == 0)
                return;

            // Update the CurrentItems based on each change that occurred
            foreach (var change in changes)
            {
                // Get the relevant data object from CurrentItems
                var dataObject = CurrentItems.OfType<DataObjectBase>().FirstOrDefault(c => c.Oid == change.Oid);
                if (dataObject == null)
                    continue;

                switch (change.ChangeType)
                {
                    case EntityChange.ChangeTypes.Modify:
                        // If the object was modified, reload it
                        dataObject.Reload();

                        break;

                    case EntityChange.ChangeTypes.Delete:
                        // If the object was deleted, remove it from CurrentItems
                        CurrentItems.Remove(dataObject);

                        break;
                }
            }

            // Update the CurrentItem
            UpdateCurrentItem();
        }

        /// <summary>
        /// Handles the CurrentItem.PropertyChanged event.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private async void CurrentItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await WritePropertyAsync(e.PropertyName);
        }

        /// <summary>
        /// Handles the CollectionChanged event for all properties on CurrentItem which are an ObservableCollection.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// <param name="property">The property containing the collection that changed.</param>
        private void CurrentItem_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e, PropertyInfo property)
        {
            SyncCollectionProperty(property, e, sender as INotifyCollectionChanged);
        }

        /// <summary>
        /// Handles the PropertyChanged event on the ChildInfo.ParentItem.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ParentItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If the property which contains the CurrentItem changes on the ParentItem
            // put the ParentItem back into CurrentItems and re-initialize the CurrentItem to get the new child
            if (e.PropertyName == ChildInfo.ChildProperty.Name)
            {
                SetCurrentItems(new[] {sender as INotifyPropertyChanged});
                UpdateCurrentItem();
            }
        }

        /// <summary>
        /// Handles the PropertyChanged event on all items in CurrentItems which are not DataObjectBase.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If one of the CurrentItems (which are not DataObjectBase) change, refresh the CurrentItem
            CurrentItem.CopyFrom(CurrentItems, false, false);
        }

        #endregion


        #region Overrides

        protected override void OnRegisterDocumentMessages(object documentId)
        {
            base.OnRegisterDocumentMessages(documentId);

            RegisterDocumentMessage<SelectedItemsChangedMessage>(documentId, OnSelectedItemsChangedMessage);
            RegisterDocumentMessage<InitializeDocumentMessage>(documentId, OnInitializeDocumentMessage);
        }

        protected override void OnUnregisterDocumentMessages(object documentId)
        {
            base.OnUnregisterDocumentMessages(documentId);

            UnregisterDocumentMessage<SelectedItemsChangedMessage>(documentId);
            UnregisterDocumentMessage<InitializeDocumentMessage>(documentId);
        }

        public override void StoreKeyedData()
        {
            base.StoreKeyedData();

            // Store the layout for the current item
            SaveItemLayout(CurrentItem);
        }

        protected override void OnWidgetClosed(EventArgs e)
        {
            base.OnWidgetClosed(e);

            // Clear CurrentItem and CurrentItems
            CurrentItem = null;
            ClearCurrentItems();

            DefaultMessenger.Unregister<EntityChangedMessage>(this, OnEntityChangedMessage);
        }

        #endregion


        #region ISupportLayoutData

        public GetLayoutDelegate GetLayout { get; set; }

        public SetLayoutDelegate SetLayout { get; set; }

        public void ApplyDefaultLayout()
        {
            byte[] defaultLayout;
            if (DefaultLayouts.TryGetValue(CurrentItem.GetType().FullName, out defaultLayout))
                SetLayout(new MemoryStream(defaultLayout));
        }

        public void ApplySavedLayout()
        {
            // Attempt to get and restore the last saved layout
            // If no saved layout was found, we will restore the default layout instead
            var savedLayout = GetKeyedData(KeyedDataScopes.Panel, KeyedDataGroupKeys.DataFormLayout, CurrentItem.GetType().FullName);
            if (savedLayout != null)
                SetLayout(savedLayout);
            else
                ApplyDefaultLayout();
        }

        #endregion
    }
}