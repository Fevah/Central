using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Core.Helper;
using TIG.TotalLink.Client.Core.Message;
using TIG.TotalLink.Client.Core.Message.Core;
using TIG.TotalLink.Client.Undo.AppContext;
using TIG.TotalLink.Client.Undo.Change;
using TIG.TotalLink.Client.Undo.Core;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Undo.Helper
{
    public class UiChangeTracker
    {
        #region Private Structs

        private struct PropertyChange
        {
            public Guid Oid;
            public string PropertyName;
            public object OldValue;
            public object NewValue;
        }

        #endregion


        #region Private Fields

        private readonly string[] _xpoProperties = { "GCRecord", "OptimisticLockField" };
        private readonly Dictionary<string, PropertyChange> _propertyChanges = new Dictionary<string, PropertyChange>();
        private readonly List<DataObjectBase> _newObjects = new List<DataObjectBase>();
        private readonly List<DataObjectBase> _deletedObjects = new List<DataObjectBase>();
        private readonly UnitOfWork _uow;
        private EntityChangedMessage _entityChangedMessage;
        private List<MonitoredUndo.Change> _batchedChanges;
        private bool _allowUndo;
        private bool _notify;

        #endregion


        #region Constructors

        public UiChangeTracker(UnitOfWork uow, object notificationSender, bool notify = true, bool allowUndo = true, bool batchChanges = false, bool flushBatchOnCommit = true)
        {
            _uow = uow;
            NotificationSender = notificationSender;
            FlushBatchOnCommit = flushBatchOnCommit;
            BatchChanges = batchChanges;
            Notify = notify;
            AllowUndo = allowUndo;

            _uow.AfterCommitTransaction += UnitOfWork_AfterCommitTransaction;
        }

        public UiChangeTracker(UnitOfWork uow, bool notify = true, bool allowUndo = true, bool batchChanges = false, bool flushOnCommit = true)
            : this(uow, null, notify, allowUndo, batchChanges, flushOnCommit)
        {
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indictates if this tracker records changes on the undo stack.
        /// </summary>
        public bool AllowUndo
        {
            get { return _allowUndo; }
            set
            {
                // Abort if the value has not changed
                if (_allowUndo == value)
                    return;

                // Apply the value
                _allowUndo = value;

                if (_allowUndo)
                {
                    // If changes are being batched, create a list to store the changes
                    if (BatchChanges)
                        _batchedChanges = new List<MonitoredUndo.Change>();

                    // Add events to the UnitOfWork
                    _uow.ObjectSaving += UnitOfWork_ObjectSaving;
                    _uow.ObjectDeleting += UnitOfWork_ObjectDeleting;
                    _uow.ObjectChanged += UnitOfWork_ObjectChanged;
                }
                else
                {
                    // If changes are being batched, flush the batched changes
                    if (BatchChanges)
                        FlushUndoBatch(false);

                    // Remove events from the UnitOfWork
                    _uow.ObjectSaving -= UnitOfWork_ObjectSaving;
                    _uow.ObjectDeleting -= UnitOfWork_ObjectDeleting;
                    _uow.ObjectChanged -= UnitOfWork_ObjectChanged;
                }
            }
        }

        /// <summary>
        /// Indicates if this tracker will notify of changes.
        /// </summary>
        public bool Notify
        {
            get { return _notify; }
            set
            {
                // Abort if the value has not changed
                if (_notify == value)
                    return;

                // Apply the value
                _notify = value;

                if (_notify)
                {
                    // If changes are being batched, create an EntityChangedMessage to batch the changes
                    if (BatchChanges)
                        _entityChangedMessage = new EntityChangedMessage(NotificationSender);

                    // Add events to the UnitOfWork
                    _uow.ObjectSaved += UnitOfWork_ObjectSaved;
                    _uow.ObjectDeleted += UnitOfWork_ObjectDeleted;
                }
                else
                {
                    // If changes ar being batched, clear the batched changes
                    if (BatchChanges)
                        FlushNotifyBatch(false);

                    // Remove events from the UnitOfWork
                    _uow.ObjectSaved -= UnitOfWork_ObjectSaved;
                    _uow.ObjectDeleted -= UnitOfWork_ObjectDeleted;
                }
            }
        }

        /// <summary>
        /// The object to use as the sender when notifications are sent.
        /// </summary>
        public object NotificationSender { get; private set; }

        /// <summary>
        /// Indicates if undos and notifications will be sent immediately when changes are written, or if they will be batched.
        /// </summary>
        public bool BatchChanges { get; private set; }

        /// <summary>
        /// Indicates if batched undos and notifications will be sent when the UnitOfWork is committed.
        /// If false, batched undos and notifications will be sent when the UnitOfWork is disposed.
        /// </summary>
        public bool FlushBatchOnCommit { get; private set; }

        #endregion


        #region Private Methods

        /// <summary>
        /// Sends an EntityChangedMessage containing all batched notify changes.
        /// </summary>
        /// <param name="reset">Indicates if the batch should be reset so it's ready to record more changes.</param>
        private void FlushNotifyBatch(bool reset)
        {
            // Send the EntityChangeMessage
            _entityChangedMessage.Send();

            // Clear the EntityChangeMessage
            _entityChangedMessage = null;

            // Create a new EntityChangeMessage, if flagged to do so
            if (reset)
                _entityChangedMessage = new EntityChangedMessage(NotificationSender);
        }

        /// <summary>
        /// Creates an undo batch containing all batched undo changes.
        /// </summary>
        /// <param name="reset">Indicates if the batch should be reset so it's ready to record more changes.</param>
        private void FlushUndoBatch(bool reset)
        {
            if (_batchedChanges != null && _batchedChanges.Count > 0)
            {
                // Generate a batch title that describes the changes
                string title = null;
                var batchedEntities = _batchedChanges.OfType<DataObjectChangeBase>().Select(c => c.DataObject).Distinct().ToList();
                if (_batchedChanges.AreSameType())
                {
                    TypeSwitch.On(_batchedChanges[0].GetType())
                        .Case<DataObjectAddChange>(() => title = ActionMessageHelper.GetTitle(batchedEntities, "add"))
                        .Case<DataObjectPropertyChange>(() => title = ActionMessageHelper.GetTitle(batchedEntities, "edit"))
                        .Case<DataObjectDeleteChange>(() => title = ActionMessageHelper.GetTitle(batchedEntities, "delete"));
                }
                else
                {
                    title = ActionMessageHelper.GetTitle(batchedEntities, "modify");
                }

                // Record all batched changes in a single undo batch
                using (new UndoBatchEx(AppUndoRootViewModel.Instance.UndoRoot, title, true))
                {
                    foreach (var change in _batchedChanges)
                    {
                        AppUndoRootViewModel.Instance.UndoRoot.AddChange(change, change.ToString());
                    }
                }
            }

            // Clear the batched changes
            _batchedChanges = null;

            // Create a new list, if flagged to do so
            if (reset)
                _batchedChanges = new List<MonitoredUndo.Change>();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the UnitOfWork.ObjectSaving event.
        /// Active when AllowUndo = true.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_ObjectSaving(object sender, ObjectManipulationEventArgs e)
        {
            // Attempt to get the UnitOfWork and data object
            var uow = (UnitOfWork)e.Session;
            var dataObject = DataModelHelper.GetDataObject(e.Object);
            if (uow == null || dataObject == null)
                return;

            if (uow.IsNewObject(dataObject))
            {
                // If the object is new, batch or add a DataObjectAddChange
                if (BatchChanges)
                {
                    _batchedChanges.Add(new DataObjectAddChange(AppUndoRootViewModel.Instance, dataObject));
                }
                else
                {
                    using (new UndoBatchEx(AppUndoRootViewModel.Instance.UndoRoot, ActionMessageHelper.GetTitle(dataObject, "add"), true))
                    {
                        AppUndoRootViewModel.Instance.ChangeFactory.OnDataObjectAdded(AppUndoRootViewModel.Instance, dataObject);
                    }
                }

                // Add the Oid to the newObjects list so we can tell which were new and which were modified in the ObjectSaved event
                _newObjects.Add(dataObject);
            }
            else
            {
                // If the object is not new, get a list of property changes for it and abort if the list is empty
                var propertyChanges = _propertyChanges.Values.Where(c => c.Oid == dataObject.Oid && !Equals(c.OldValue, c.NewValue)).ToList();
                if (propertyChanges.Count == 0)
                    return;

                // Batch or add a list of DataObjectPropertyChange
                if (BatchChanges)
                {
                    foreach (var change in propertyChanges)
                    {
                        _batchedChanges.Add(new DataObjectPropertyChange(AppUndoRootViewModel.Instance, dataObject, change.PropertyName, change.OldValue, change.NewValue));
                    }
                }
                else
                {
                    using (new UndoBatchEx(AppUndoRootViewModel.Instance.UndoRoot, ActionMessageHelper.GetTitle(dataObject, "edit"), true))
                    {
                        foreach (var change in propertyChanges)
                        {
                            AppUndoRootViewModel.Instance.ChangeFactory.OnDataObjectPropertyChanged(AppUndoRootViewModel.Instance, dataObject, change.PropertyName, change.OldValue, change.NewValue);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles the UnitOfWork.ObjectDeleting event.
        /// Active when AllowUndo = true.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_ObjectDeleting(object sender, ObjectManipulationEventArgs e)
        {
            // Attempt to get the data object
            var dataObject = DataModelHelper.GetDataObject(e.Object);
            if (dataObject == null)
                return;

            // Batch or add a DataObjectDeleteChange
            if (BatchChanges)
            {
                _batchedChanges.Add(new DataObjectDeleteChange(AppUndoRootViewModel.Instance, dataObject));
            }
            else
            {
                using (new UndoBatchEx(AppUndoRootViewModel.Instance.UndoRoot, ActionMessageHelper.GetTitle(dataObject, "delete"), true))
                {
                    AppUndoRootViewModel.Instance.ChangeFactory.OnDataObjectDeleted(AppUndoRootViewModel.Instance, dataObject);
                }
            }
        }

        /// <summary>
        /// Handles the UnitOfWork.ObjectChanged event.
        /// Active when AllowUndo = true.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_ObjectChanged(object sender, ObjectChangeEventArgs e)
        {
            // Abort if the value has not changed
            if (Equals(e.OldValue, e.NewValue))
                return;

            // Attempt to get the UnitOfWork and data object
            var uow = (UnitOfWork)e.Session;
            var dataObject = DataModelHelper.GetDataObject(e.Object);
            if (uow == null || dataObject == null)
                return;

            // Abort if the UnitOfWork is currently loading or saving, or the object is new
            if (uow.IsObjectsLoading || uow.IsObjectsSaving || uow.IsNewObject(dataObject))
                return;

            // Abort if the property is any of the XPO system properties
            if (_xpoProperties.Contains(e.PropertyName))
                return;

            // Attempt to find an existing change for this property
            var changeKey = string.Format("{0}|{1}", dataObject.Oid, e.PropertyName);
            PropertyChange change;
            if (_propertyChanges.TryGetValue(changeKey, out change))
            {
                // If an existing change was found, just update the NewValue
                change.NewValue = e.NewValue;
            }
            else
            {
                // If no existing change was found, create and add a new one
                _propertyChanges.Add(changeKey, new PropertyChange()
                {
                    Oid = dataObject.Oid,
                    PropertyName = e.PropertyName,
                    OldValue = e.OldValue,
                    NewValue = e.NewValue
                });
            }
        }

        /// <summary>
        /// Handles the UnitOfWork.ObjectSaved event.
        /// Active when Notify = true.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_ObjectSaved(object sender, ObjectManipulationEventArgs e)
        {
            // Attempt to get the data object
            var dataObject = DataModelHelper.GetDataObject(e.Object);
            if (dataObject == null)
                return;

            // Initially assume the ChangeType is Modify
            var changeType = EntityChange.ChangeTypes.Modify;

            // If the object was deleted, set the ChangeType to Delete and remove the item from the deletedObjects list
            var deletedObjectIndex = _deletedObjects.IndexOf(dataObject);
            if (deletedObjectIndex > -1)
            {
                _deletedObjects.RemoveAt(deletedObjectIndex);
                changeType = EntityChange.ChangeTypes.Delete;
            }

            // If the object was new, set the ChangeType to Add and remove the item from the newObjects list
            var newObjectIndex = _newObjects.IndexOf(dataObject);
            if (newObjectIndex > -1)
            {
                _newObjects.RemoveAt(newObjectIndex);
                changeType = EntityChange.ChangeTypes.Add;
            }

            // Batch or send the EntityChanged message
            if (BatchChanges)
                _entityChangedMessage.AddChange(dataObject, changeType);
            else
                EntityChangedMessage.Send(NotificationSender, dataObject, changeType);
        }

        /// <summary>
        /// Handles the UnitOfWork.ObjectDeleted event.
        /// Active when Notify = true.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_ObjectDeleted(object sender, ObjectManipulationEventArgs e)
        {
            // Attempt to get the data object
            var dataObject = DataModelHelper.GetDataObject(e.Object);
            if (dataObject == null)
                return;

            // Add the data object to the deletedObjects list
            _deletedObjects.Add(dataObject);
        }

        /// <summary>
        /// Handles the UnitOfWork.AfterCommitTransaction event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void UnitOfWork_AfterCommitTransaction(object sender, SessionManipulationEventArgs e)
        {
            if (!BatchChanges || !FlushBatchOnCommit)
                return;

            if (AllowUndo)
                FlushUndoBatch(true);

            if (Notify)
                FlushNotifyBatch(true);
        }

        #endregion
    }
}
