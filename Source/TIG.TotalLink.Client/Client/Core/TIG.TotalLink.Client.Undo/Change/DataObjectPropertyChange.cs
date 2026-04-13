using MonitoredUndo;
using TIG.TotalLink.Client.Undo.Extension;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Undo.Change
{
    public class DataObjectPropertyChange : DataObjectChangeBase
    {
        #region Constructors

        public DataObjectPropertyChange(object target, DataObjectBase dataObject, string propertyName, object oldValue, object newValue)
            : base(target, new ChangeKey<object, DataObjectBase, string>(target, dataObject, propertyName))
        {
            DataObject = dataObject;
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The name of the property that was modified.
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// The original value of the property.
        /// </summary>
        public object OldValue { get; private set; }

        /// <summary>
        /// The new value of the property.
        /// </summary>
        public object NewValue { get; private set; }

        #endregion


        #region Overrides

        public override void MergeWith(MonitoredUndo.Change latestChange)
        {
            var propertyChange = latestChange as DataObjectPropertyChange;
            if (propertyChange == null)
                return;
            NewValue = propertyChange.NewValue;
        }

        protected override void PerformUndo()
        {
            DataObject.SetDataProperty(PropertyName, OldValue, false);
        }

        protected override void PerformRedo()
        {
            DataObject.SetDataProperty(PropertyName, NewValue, false);
        }

        public override string ToString()
        {
            return string.Format("{0} (DataObject={1}, Property={2}, OldValue={3}, NewValue={4})", GetType().Name, DataObject, PropertyName, OldValue, NewValue);
        }

        #endregion
    }
}
