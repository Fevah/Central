namespace TIG.TotalLink.Client.Undo.Change
{
    public class PropertyChange : MonitoredUndo.PropertyChange
    {
        #region Constructors

        public PropertyChange(object target, string propertyName, object oldValue, object newValue)
            : base(target, propertyName, oldValue, newValue)
        {
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format("{0} (Property={1}, OldValue={2}, NewValue={3})", GetType().Name, PropertyName, OldValue, NewValue);
        }

        #endregion
    }
}
