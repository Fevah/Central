using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using TIG.TotalLink.Client.Undo.Change;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Undo.Core
{
    public class ChangeFactoryEx : MonitoredUndo.ChangeFactory
    {
        #region Public Properties

        /// <summary>
        /// Indicates if actions will be tracked on the undo stack.
        /// </summary>
        public bool TrackUndo { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a DataObjectAddChange to the undo stack.
        /// </summary>
        /// <param name="instance">The scope to record this undo in.</param>
        /// <param name="dataObject">The data object that was added.</param>
        public virtual void OnDataObjectAdded(object instance, DataObjectBase dataObject)
        {
            OnDataObjectAdded(instance, dataObject, null);
        }

        /// <summary>
        /// Adds a DataObjectAddChange to the undo stack.
        /// </summary>
        /// <param name="instance">The scope to record this undo in.</param>
        /// <param name="dataObject">The data object that was added.</param>
        /// <param name="descriptionOfChange">Description of the change that was made.</param>
        public virtual void OnDataObjectAdded(object instance, DataObjectBase dataObject, string descriptionOfChange)
        {
            // Get the undo root
            var undoRoot = GetUndoRoot(instance);
            if (undoRoot == null)
                return;

            // Store the change
            var change = new DataObjectAddChange(instance, dataObject);
            undoRoot.AddChange(change, descriptionOfChange ?? change.ToString());
        }

        /// <summary>
        /// Adds a DataObjectDeleteChange to the undo stack.
        /// </summary>
        /// <param name="instance">The scope to record this undo in.</param>
        /// <param name="dataObject">The data object that was deleted.</param>
        public virtual void OnDataObjectDeleted(object instance, DataObjectBase dataObject)
        {
            OnDataObjectDeleted(instance, dataObject, null);
        }

        /// <summary>
        /// Adds a DataObjectDeleteChange to the undo stack.
        /// </summary>
        /// <param name="instance">The scope to record this undo in.</param>
        /// <param name="dataObject">The data object that was deleted.</param>
        /// <param name="descriptionOfChange">Description of the change that was made.</param>
        public virtual void OnDataObjectDeleted(object instance, DataObjectBase dataObject, string descriptionOfChange)
        {
            // Get the undo root
            var undoRoot = GetUndoRoot(instance);
            if (undoRoot == null)
                return;

            // Store the change
            var change = new DataObjectDeleteChange(instance, dataObject);
            undoRoot.AddChange(change, descriptionOfChange ?? change.ToString());
        }

        /// <summary>
        /// Adds a DataObjectPropertyChange to the undo stack.
        /// </summary>
        /// <param name="instance">The scope to record this undo in.</param>
        /// <param name="dataObject">The data object that was modified.</param>
        /// <param name="propertyName">The name of the property that was modified.</param>
        /// <param name="oldValue">The original value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        public virtual void OnDataObjectPropertyChanged(object instance, DataObjectBase dataObject, string propertyName, object oldValue, object newValue)
        {
            OnDataObjectPropertyChanged(instance, dataObject, propertyName, oldValue, newValue, null);
        }

        /// <summary>
        /// Adds a DataObjectPropertyChange to the undo stack.
        /// </summary>
        /// <param name="instance">The scope to record this undo in.</param>
        /// <param name="dataObject">The data object that was modified.</param>
        /// <param name="propertyName">The name of the property that was modified.</param>
        /// <param name="oldValue">The original value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        /// <param name="descriptionOfChange">Description of the change that was made.</param>
        public virtual void OnDataObjectPropertyChanged(object instance, DataObjectBase dataObject, string propertyName, object oldValue, object newValue, string descriptionOfChange)
        {
            // Get the undo root
            var undoRoot = GetUndoRoot(instance);
            if (undoRoot == null)
                return;

            // Store the change
            var change = new DataObjectPropertyChange(instance, dataObject, propertyName, oldValue, newValue);
            undoRoot.AddChange(change, descriptionOfChange ?? change.ToString());
        }

        #endregion


        #region Private Methods

        private MonitoredUndo.UndoRoot GetUndoRoot(object instance)
        {
            // Abort if undo tracking is disabled
            if (!TrackUndo)
                return null;

            // Attempt to get the instance as an ISupportsUndo
            var supportsUndo = instance as MonitoredUndo.ISupportsUndo;
            if (supportsUndo == null)
                return null;

            // Attempt to get the root object
            var undoRootObject = supportsUndo.GetUndoRoot();
            if (undoRootObject == null)
                return null;

            // Get the undo root for the root object
            return MonitoredUndo.UndoService.Current[undoRootObject];
        }

        #endregion


        #region Overrides

        public override void OnChanging(object instance, string propertyName, object oldValue, object newValue, string descriptionOfChange)
        {
            // Abort if undo tracking is disabled
            if (!TrackUndo)
                return;

            // Store the changes
            base.OnChanging(instance, propertyName, oldValue, newValue, descriptionOfChange);
        }

        public override void OnCollectionChanged(object instance, string propertyName, object collection, NotifyCollectionChangedEventArgs e, string descriptionOfChange)
        {
            // Abort if undo tracking is disabled
            if (!TrackUndo)
                return;

            // Store the changes
            base.OnCollectionChanged(instance, propertyName, collection, e, descriptionOfChange);
        }

        public override MonitoredUndo.Change GetChange(object instance, string propertyName, object oldValue, object newValue)
        {
            var undoMetadata = instance as MonitoredUndo.IUndoMetadata;
            if (null != undoMetadata)
            {
                if (!undoMetadata.CanUndoProperty(propertyName, oldValue, newValue))
                    return null;
            }

            var change = new PropertyChange(instance, propertyName, oldValue, newValue);

            return change;
        }

        public override IList<MonitoredUndo.Change> GetCollectionChange(object instance, string propertyName, object collection, NotifyCollectionChangedEventArgs e)
        {
            var undoMetadata = instance as MonitoredUndo.IUndoMetadata;
            if (null != undoMetadata)
            {
                if (!undoMetadata.CanUndoCollectionChange(propertyName, collection, e))
                    return null;
            }

            var ret = new List<MonitoredUndo.Change>();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems)
                    {
                        MonitoredUndo.Change change = null;
                        var list = collection as IList;
                        if (list != null)
                        {
                            change = new CollectionAddChange(instance, propertyName, list, e.NewStartingIndex, item);
                        }
                        //else if (collection as IDictionary != null)
                        //{
                        //    // item is a key value pair - get key and value to be recorded in dictionary change
                        //    var keyProperty = item.GetType().GetProperty("Key");
                        //    var valueProperty = item.GetType().GetProperty("Value");
                        //    change = new DictionaryAddChange(instance, propertyName, (IDictionary)collection,
                        //                                         keyProperty.GetValue(item, null), valueProperty.GetValue(item, null));
                        //}
                        ret.Add(change);
                    }

                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems)
                    {
                        MonitoredUndo.Change change = null;
                        var list = collection as IList;
                        if (list != null)
                        {
                            change = new CollectionRemoveChange(instance, propertyName, list, e.OldStartingIndex, item);
                        }
                        //else if (collection as IDictionary != null)
                        //{
                        //    // item is a key value pair - get key and value to be recorded in dictionary change
                        //    var keyProperty = item.GetType().GetProperty("Key");
                        //    var valueProperty = item.GetType().GetProperty("Value");
                        //    change = new DictionaryRemoveChange(instance, propertyName, (IDictionary)collection,
                        //                                         keyProperty.GetValue(item, null), valueProperty.GetValue(item, null));
                        //}
                        ret.Add(change);
                    }

                    break;

                case NotifyCollectionChangedAction.Replace:
                    for (var i = 0; i < e.OldItems.Count; i++)
                    {
                        MonitoredUndo.Change change = null;

                        var list = collection as IList;
                        if (list != null)
                        {
                            change = new CollectionReplaceChange(instance, propertyName, list, e.NewStartingIndex, e.OldItems[i], e.NewItems[i]);
                        }
                        //else if (collection as IDictionary != null)
                        //{
                        //    // item is a key value pair - get key and value to be recorded in dictionary change
                        //    var keyProperty = e.OldItems[i].GetType().GetProperty("Key");
                        //    var oldValueProperty = e.OldItems[i].GetType().GetProperty("Value");
                        //    var newValueProperty = e.OldItems[i].GetType().GetProperty("Value");
                        //    change = new DictionaryReplaceChange(
                        //        instance, propertyName, (IDictionary)collection, keyProperty.GetValue(e.OldItems[i], null), oldValueProperty.GetValue(e.OldItems[i], null), newValueProperty.GetValue(e.NewItems[i], null));
                        //}
                        ret.Add(change);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    if (ThrowExceptionOnCollectionResets)
                        throw new NotSupportedException("Undoing collection resets is not supported via the CollectionChanged event. The collection is already null, so the Undo system has no way to capture the set of elements that were previously in the collection.");

                    break;

                default:
                    throw new NotSupportedException();
            }

            return ret;
        }

        #endregion
    }
}
