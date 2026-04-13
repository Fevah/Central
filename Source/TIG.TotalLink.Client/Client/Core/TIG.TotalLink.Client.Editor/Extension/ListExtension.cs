using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using TIG.TotalLink.Shared.DataModel.Core;

namespace TIG.TotalLink.Client.Editor.Extension
{
    public static class ListExtension
    {
        /// <summary>
        /// Synchronizes the contents of one list with another.
        /// </summary>
        /// <param name="srcList">The source list to synchronize from.</param>
        /// <param name="destList">The destination list to synchronize to.</param>
        public static void SyncTo(this IList srcList, IList destList)
        {
            // ABort if the destList is null
            if (destList == null)
                return;

            // If the source list is null or empty, just clear the dest list
            if (srcList == null || srcList.Count == 0)
            {
                if (destList.Count > 0)
                    destList.Clear();
                return;
            }

            // Cast the lists to List<object> so we can perform linq queries on them
            var srcListAsObject = srcList.Cast<object>().ToList();
            var destListAsObject = destList.Cast<object>().ToList();

            // Add each item to the dest list that only exists in the source list
            foreach (var addItem in srcListAsObject.Where(o1 => !destListAsObject.Any(o2 => Equals(o1, o2))).ToList())
            {
                destList.Add(addItem);
            }

            // Remove each item from the dest list that only exists in the dest list
            foreach (var removeItem in destListAsObject.Where(o1 => !srcListAsObject.Any(o2 => Equals(o1, o2))).ToList())
            {
                destList.Remove(removeItem);
            }
        }

        /// <summary>
        /// Syncronizes two lists by applying changes to the destination list, using data from a source list event.
        /// </summary>
        /// <param name="destList">The destination list to synchronize to.</param>
        /// <param name="e">A NotifyCollectionChangedEventArgs object received from the source list event.</param>
        public static void SyncChanges(this IList destList, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Reset:
                    destList.Clear();
                    break;

                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems)
                    {
                        var dataObject = item as DataObjectBase;
                        if (dataObject != null)
                        {
                            if (destList.OfType<DataObjectBase>().All(o => o.Oid != dataObject.Oid))
                                destList.Add(item);
                        }
                        else
                        {
                            if (!destList.Contains(item))
                                destList.Add(item);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems)
                    {
                        var dataObject = item as DataObjectBase;
                        if (dataObject != null)
                        {
                            var o = destList.OfType<DataObjectBase>().FirstOrDefault(i => i.Oid == dataObject.Oid);
                            if (o != null)
                                destList.Remove(o);
                        }
                        else
                        {
                            var index = destList.IndexOf(item);
                            if (index > -1)
                                destList.RemoveAt(index);
                        }
                    }
                    break;
            }
        }
    }
}
