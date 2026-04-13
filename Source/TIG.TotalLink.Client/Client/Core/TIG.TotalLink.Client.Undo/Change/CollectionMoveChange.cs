using System.Collections;

namespace TIG.TotalLink.Client.Undo.Change
{
    public class CollectionMoveChange : MonitoredUndo.CollectionMoveChange
    {
        #region Constructors

        public CollectionMoveChange(object target, string propertyName, IList collection, int newIndex, int oldIndex)
            : base(target, propertyName, collection, newIndex, oldIndex)
        {
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format("{0} (Property={1}, OldIndex={2}, NewIndex={3})", GetType().Name, PropertyName, OldIndex, NewIndex);
        }

        #endregion
    }
}
