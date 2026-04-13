using System;
using System.IO;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Module.Admin.Enum.KeyedData;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Admin;

namespace TIG.TotalLink.Client.Module.Admin.Helper
{
    public class KeyedDataHelper
    {
        /// <summary>
        /// Adds or updates keyed data from a byte array.
        /// </summary>
        /// <param name="dataCollection">The collection to update.  Must be an XPBaseCollection containing KeyedDataBase entities.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <param name="data">A byte array containing the data.</param>
        public static void SetData(XPBaseCollection dataCollection, KeyedDataGroupKeys groupKey, string itemKey, byte[] data)
        {
            // Make sure the collection is an XPCollection and get the type of entity it contains
            if (dataCollection == null || !typeof(XPCollection<>).IsAssignableFromGeneric(dataCollection.GetType()))
                return;
            var entityType = dataCollection.GetType().GenericTypeArguments[0];

            // Attempt to find an existing KeyedData
            var groupKeyString = groupKey.ToString();
            var keyedData = dataCollection.OfType<KeyedDataBase>().FirstOrDefault(d => d.GroupKey == groupKeyString && d.ItemKey == itemKey);
            if (keyedData != null)
            {
                // An existing KeyedData was found, so update it with the new data
                keyedData.Data = data;
            }
            else
            {
                // No existing KeyedData was found, so create a new one
                keyedData = DataObjectHelper.CreateDataObject(entityType, dataCollection.Session) as KeyedDataBase;
                if (keyedData == null)
                    throw new Exception(string.Format("Failed to set keyed data!\r\nCould not create new keyed data object of type {0}.", entityType.Name));

                // Update the KeyedData properties
                keyedData.GroupKey = groupKeyString;
                keyedData.ItemKey = itemKey;
                keyedData.Data = data;

                // Add the KeyedData to the collection
                dataCollection.BaseAdd(keyedData);
            }
        }

        /// <summary>
        /// Adds or updates keyed data from a stream.
        /// </summary>
        /// <param name="dataCollection">The collection to update.  Must be an XPBaseCollection containing KeyedDataBase entities.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <param name="data">A Stream containing the data.</param>
        /// <param name="closeStream">Indicates if the stream should be closed.</param>
        public static void SetData(XPBaseCollection dataCollection, KeyedDataGroupKeys groupKey, string itemKey, Stream data, bool closeStream)
        {
            // Copy the data to a new MemoryStream
            using (var newData = new MemoryStream())
            {
                data.CopyTo(newData);
                newData.Seek(0, SeekOrigin.Begin);

                // Close the stream if specified
                if (closeStream)
                    data.Close();

                // Store the data
                SetData(dataCollection, groupKey, itemKey, newData.ToArray());
            }
        }

        /// <summary>
        /// Gets keyed data.
        /// </summary>
        /// <param name="dataCollection">The collection to find the keyed data in.  Must be an XPBaseCollection containing KeyedDataBase entities.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>
        /// A new MemoryStream containing the data.
        /// After the stream has been used, it should be disposed.
        /// </returns>
        public static MemoryStream GetData(XPBaseCollection dataCollection, KeyedDataGroupKeys groupKey, string itemKey)
        {
            // Attempt to find an existing KeyedData
            var groupKeyString = groupKey.ToString();
            var keyedData = dataCollection.OfType<KeyedDataBase>().FirstOrDefault(d => d.GroupKey == groupKeyString && d.ItemKey == itemKey);

            // If data was found, return it as a stream
            if (keyedData != null && keyedData.Data != null)
                return new MemoryStream(keyedData.Data);

            // If no data was found, return null
            return null;
        }

        /// <summary>
        /// Removes keyed data.
        /// </summary>
        /// <param name="dataCollection">The collection to remove the keyed data from.  Must be an XPBaseCollection containing KeyedDataBase entities.</param>
        /// <param name="groupKey">A string key that defines the type of data being stored.</param>
        /// <param name="itemKey">A string key that defines a unique name within the group for the data item.</param>
        /// <returns>True if the widget data was found and removed successfully; otherwise false.</returns>
        public static bool RemoveData(XPBaseCollection dataCollection, KeyedDataGroupKeys groupKey, string itemKey)
        {
            // Attempt to find an existing KeyedData
            var groupKeyString = groupKey.ToString();
            var keyedData = dataCollection.OfType<KeyedDataBase>().FirstOrDefault(d => d.GroupKey == groupKeyString && d.ItemKey == itemKey);

            // If data was found, delete it and return true
            if (keyedData != null)
            {
                keyedData.Delete();
                return true;
            }

            // Otherwise return false
            return false;
        }
    }
}
