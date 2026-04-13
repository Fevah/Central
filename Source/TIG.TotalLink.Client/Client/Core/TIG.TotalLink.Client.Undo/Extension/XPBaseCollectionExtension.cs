using System.Collections;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Undo.Extension
{
    public static class XPBaseCollectionExtension
    {
        /// <summary>
        /// Updates the <paramref name="targetList"/> to match the <paramref name="sourceList"/>.
        /// </summary>
        /// <param name="targetList">The list to synchronize to.</param>
        /// <param name="sourceList">The list to synchronize from.</param>
        public static void SyncXpCollectionFrom(this XPBaseCollection targetList, XPBaseCollection sourceList)
        {
            // Remove all items that exist in targetList, but not in sourceList
            for (var i = targetList.Count - 1; i >= 0; i--)
            {
                // Attempt to get the target item as a DataObjectBase
                var targetDataObject = DataModelHelper.GetDataObject(((IList)targetList)[i]);
                if (targetDataObject == null)
                    continue;

                // If there is no item in the sourceList with an Oid which matches targetDataObject.Oid, then remove the item from the targetList
                if (sourceList.OfType<DataObjectBase>().All(o => o.Oid != targetDataObject.Oid))
                    ((IList)targetList).RemoveAt(i);
            }

            // Add all items that exist in sourceList, but not in targetList
            foreach (var item in sourceList.OfType<DataObjectBase>().ToList())
            {
                // Attempt to get the source item as a DataObjectBase
                var sourceDataObject = DataModelHelper.GetDataObject(item);
                if (sourceDataObject == null)
                    continue;

                // If there is no item in the targetList with an Oid which matches sourceDataObject.Oid, then add the item to the targetList
                if (targetList.OfType<DataObjectBase>().All(o => o.Oid != sourceDataObject.Oid))
                {
                    ((IList)targetList).Add(((UnitOfWork)targetList.Session).GetDataObject(sourceDataObject, sourceDataObject.GetType()));
                }
            }
        }
    }
}
