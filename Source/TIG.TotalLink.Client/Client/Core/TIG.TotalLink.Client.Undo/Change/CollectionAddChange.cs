using System.Collections;

namespace TIG.TotalLink.Client.Undo.Change
{
    public class CollectionAddChange : MonitoredUndo.CollectionAddChange
    {
        #region Constructors

        public CollectionAddChange(object target, string propertyName, IList collection, int index, object element)
            : base(target, propertyName, collection, index, element)
        {
        }

        #endregion


        #region Overrides

        public override string ToString()
        {
            return string.Format("{0} (Property={1}, Index={2}, Item={3})", GetType().Name, PropertyName, Index, Element);
        }

        #endregion
    }
}
