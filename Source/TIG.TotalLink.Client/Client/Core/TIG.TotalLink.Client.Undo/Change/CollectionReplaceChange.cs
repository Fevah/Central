using System.Collections;

namespace TIG.TotalLink.Client.Undo.Change
{
    public class CollectionReplaceChange : MonitoredUndo.CollectionReplaceChange
    {
        #region Constructors

        public CollectionReplaceChange(object target, string propertyName, IList collection, int index, object oldItem, object newItem)
            : base(target, propertyName, collection, index, oldItem, newItem)
        {
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format("{0} (Property={1}, Index={2}, OldItem={3}, NewItem={4})", GetType().Name, PropertyName, Index, OldItem, NewItem);
        }

        #endregion
    }
}
