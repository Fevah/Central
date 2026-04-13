using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Undo.Extension
{
    public static class ObjectExtension
    {
        /// <summary>
        /// Copies all values from the objects in the supplied list to this object.
        /// </summary>
        /// <param name="obj">The object to copy values to.</param>
        /// <param name="sourceList">The list of objects to copy values from.</param>
        /// <param name="editorPropertiesOnly">
        /// If true, only properties that will be displayed in generated editors will be copied.
        /// if false, all properties will be copied.
        /// </param>
        /// <param name="copyPersistentCollections">Indicates if the contents of persistent collections should be copied.</param>
        public static void CopyFrom(this object obj, IList sourceList, bool editorPropertiesOnly = false, bool copyPersistentCollections = true)
        {
            var propertyWrappers = editorPropertiesOnly
                ? obj.GetType().GetVisibleAndAliasedProperties()
                : obj.GetType().GetSupportedAndAliasedProperties(true);

            if (propertyWrappers == null)
                return;

            // Copy all properties from the source list to the target object
            foreach (var propertyWrapper in propertyWrappers)
            {
                // Attempt to get the PropertyDescriptor for the property
                var propertyDescriptor = propertyWrapper.Property.ContextObject as PropertyDescriptor;
                if (propertyDescriptor != null)
                {
                    // Skip the property if it has a DoNotCopyAttribute
                    if (!propertyDescriptor.Attributes.OfType<DoNotCopyAttribute>().Any())
                    {
                        // If the property is XPBaseCollection then we need to copy the contents of the collection
                        if (typeof (XPBaseCollection).IsAssignableFrom(propertyWrapper.Property.PropertyType))
                        {
                            // Only copy the collection if not requested to skip them
                            if (copyPersistentCollections)
                                ((XPBaseCollection) propertyDescriptor.GetValue(obj)).SyncXpCollectionFrom((XPBaseCollection) propertyDescriptor.GetValue(sourceList[0]));
                        }
                        else // Otherwise, just copy the property value
                        {
                            // Ignore the property if it doesn't have a setter
                            if (propertyDescriptor.IsReadOnly)
                                continue;

                            // Collect the property value
                            var value = sourceList.OfType<object>().Select(o => propertyDescriptor.GetValue(o)).ValueIfEqualOrDefault();

                            var dataObject = DataModelHelper.GetDataObject(obj);
                            if (dataObject != null)
                            {
                                // If the value is a DataObjectBase, get a copy of it in the target session
                                if (value != null && typeof (DataObjectBase).IsAssignableFrom(propertyWrapper.Property.PropertyType))
                                    value = ((UnitOfWork) dataObject.Session).GetDataObject(value, value.GetType());

                                // Write the property value
                                dataObject.SetDataProperty(propertyWrapper.Property.Name, value, false, false);
                            }
                            else
                            {
                                // If the object is not a DataObjectBase, just update the property directly
                                propertyDescriptor.SetValue(obj, value);
                            }
                        }
                    }
                }

                // If the property contains an alias, we need to copy the value of the aliased display field also
                if (propertyWrapper.ContainsAlias)
                {
                    // If the alias type is XPBaseCollection then we need to copy the contents of the collection
                    if (typeof(XPBaseCollection).IsAssignableFrom(propertyWrapper.Alias.TargetFieldType))
                    {
                        // Only copy the collection if not requested to skip them
                        if (copyPersistentCollections)
                            ((XPBaseCollection)propertyWrapper.Alias.GetValue(obj)).SyncXpCollectionFrom((XPBaseCollection)propertyWrapper.Alias.GetValue(sourceList[0]));
                    }
                    else // Otherwise, just copy the property value
                    {
                        // Collect the property value
                        var propertyWrapper1 = propertyWrapper;
                        var value = sourceList.OfType<object>().Select(o => propertyWrapper1.Alias.GetValue(o)).ValueIfEqualOrDefault();

                        // Write the property value
                        propertyWrapper.Alias.SetValue(obj, value);
                    }
                }
            }
        }
    }
}
