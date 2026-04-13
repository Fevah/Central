using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using DevExpress.Entity.Model;
using DevExpress.Mvvm;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Client.Undo.Helper;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Client.Undo.Extension
{
    public static class DataObjectBaseExtension
    {
        /// <summary>
        /// Gets an instance of the facade that manages this data object.
        /// </summary>
        /// <param name="dataObject">The data object to get the related facade for.</param>
        /// <returns>The facade that manages this data object.</returns>
        public static IFacadeBase GetFacade(this DataObjectBase dataObject)
        {
            return DataObjectHelper.GetFacade(dataObject.GetType());
        }

        /// <summary>
        /// Helper method to update a property value.
        /// This method will always write the change, store it on the undo stack and notify widgets.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value.</typeparam>
        /// <param name="dataObject">The data object being updated.</param>
        /// <param name="newValue">The new value for the property.</param>
        /// <param name="expression">An expression which describes the property being updated.</param>
        /// <param name="changedCallback">A function to call when the property is modified.</param>
        public static void SetDataProperty<TValue>(this DataObjectBase dataObject, TValue newValue, Expression<Func<TValue>> expression, Action changedCallback = null)
        {
            SetDataProperty(dataObject, BindableBase.GetPropertyName(expression), newValue, changedCallback);
        }

        /// <summary>
        /// Helper method to update a property value.
        /// This method will always write the change, store it on the undo stack and notify widgets.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value.</typeparam>
        /// <param name="dataObject">The data object being updated.</param>
        /// <param name="propertyName">The name of the property to update.</param>
        /// <param name="newValue">The new value for the property.</param>
        /// <param name="changedCallback">A function to call when the property is modified.</param>
        public static void SetDataProperty<TValue>(this DataObjectBase dataObject, string propertyName, TValue newValue, Action changedCallback = null)
        {
            SetDataProperty(dataObject, propertyName, newValue, true, true, null, changedCallback);
        }

        /// <summary>
        /// Helper method to update a property value.
        /// This method will optionally store the change on the undo stack and write the changes.
        /// </summary>
        /// <param name="dataObject">The data object being updated.</param>
        /// <param name="propertyName">The name of the property to update.</param>
        /// <param name="newValue">The new value for the property.</param>
        /// <param name="allowUndo">Indicates if the change should be recorded on the undo stack.</param>
        /// <param name="write">If true, modifications will be immediately written to the database.</param>
        /// <param name="notificationSender">The sender to use when sending notifications.</param>
        /// <param name="changedCallback">A function to call after the property is modified.</param>
        public static void SetDataProperty(this DataObjectBase dataObject, string propertyName, object newValue, bool allowUndo = true, bool write = true, object notificationSender = null, Action changedCallback = null)
        {
            // Attempt to find the property to update
            var property = dataObject.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                return;

            var valueUpdated = false;
            if (write)
            {
                // Attempt to get a facade to write the change
                var facade = dataObject.GetFacade();
                if (facade == null)
                    throw new Exception(string.Format("Failed to find facade to write changes to a {0}", dataObject.GetType().Name));

                facade.ExecuteUnitOfWork(uow =>
                {
                    uow.StartUiTracking(notificationSender, true, allowUndo);

                    // Get a copy of the data object in this session
                    var sessionDataObject = uow.GetDataObject(dataObject, dataObject.GetType());
                    if (sessionDataObject == null)
                        return;

                    // Get the old property value, and abort if it is the same as the new value
                    var oldValue = property.GetValue(sessionDataObject);
                    if (Equals(oldValue, newValue))
                        return;

                    // If the new value is a data object, get a copy of it in this session
                    var newValueDataObject = newValue as DataObjectBase;
                    if (newValueDataObject != null)
                        newValue = uow.GetDataObject(newValue, newValue.GetType());

                    // Set the property to the new value
                    property.SetValue(sessionDataObject, newValue);
                    valueUpdated = true;
                });
            }
            else
            {
                // Get the old property value, and abort if it is the same as the new value
                var oldValue = property.GetValue(dataObject);
                if (Equals(oldValue, newValue))
                    return;

                // If the new value is a data object, get a copy of it in the session of the object being updated
                var newValueDataObject = newValue as DataObjectBase;
                if (newValueDataObject != null)
                    newValue = ((UnitOfWork)(dataObject.Session)).GetDataObject(newValue, newValue.GetType());

                // Set the property to the new value
                property.SetValue(dataObject, newValue);
                valueUpdated = true;
            }

            // Call the changedCallback if one was supplied
            if (changedCallback != null && valueUpdated)
                changedCallback();
        }

        /// <summary>
        /// Helper method to delete a data object.
        /// This method will optionally store the change on the undo stack and write the changes.
        /// </summary>
        /// <param name="dataObject">The data object being deleted.</param>
        /// <param name="allowUndo">Indicates if the change should be recorded on the undo stack.</param>
        /// <param name="write">If true, modifications will be immediately written to the database.</param>
        /// <param name="notificationSender">The sender to use when sending notifications.</param>
        public static void DeleteDataObject(this DataObjectBase dataObject, bool allowUndo = true, bool write = true, object notificationSender = null)
        {
            if (write)
            {
                // Attempt to get a facade to write the change
                var facade = dataObject.GetFacade();
                if (facade == null)
                    throw new Exception(string.Format("Failed to find facade to delete a {0}", dataObject.GetType().Name));

                facade.ExecuteUnitOfWork(uow =>
                {
                    uow.StartUiTracking(notificationSender, true, allowUndo);

                    // Get a copy of the data object in this session
                    var sessionDataObject = uow.GetDataObject(dataObject, dataObject.GetType());
                    if (sessionDataObject == null)
                        return;

                    // Delete the object
                    sessionDataObject.Delete();
                });
            }
            else
            {
                // Delete the object
                dataObject.Delete();
            }
        }

        /// <summary>
        /// Helper method to asynchronously delete a data object immediately.
        /// This method will optionally store the change on the undo stack.
        /// </summary>
        /// <param name="dataObject">The data object being deleted.</param>
        /// <param name="allowUndo">Indicates if the change should be recorded on the undo stack.</param>
        /// <param name="notificationSender">The sender to use when sending notifications.</param>
        public static async Task DeleteDataObjectAsync(this DataObjectBase dataObject, bool allowUndo = true, object notificationSender = null)
        {
            // Attempt to get a facade to write the change
            var facade = dataObject.GetFacade();
            if (facade == null)
                throw new Exception(string.Format("Failed to find facade to delete a {0}", dataObject.GetType().Name));

            await facade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(notificationSender, true, allowUndo);

                // Get a copy of the data object in this session
                var sessionDataObject = uow.GetDataObject(dataObject, dataObject.GetType());
                if (sessionDataObject == null)
                    return;

                // Delete the object
                sessionDataObject.Delete();
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Helper method to duplicate a data object and write it immediately.
        /// This method will optionally store the change on the undo stack and notify widgets.
        /// </summary>
        /// <param name="dataObject">The data object being duplicated.</param>
        /// <param name="allowUndo">Indicates if the change should be recorded on the undo stack.</param>
        /// <param name="notify">Indicates if widgets should be notified about the change.</param>
        /// <param name="notificationSender">The sender to use when sending notifications.</param>
        /// <returns>The new duplicate data object.</returns>
        public static DataObjectBase DuplicateDataObject(this DataObjectBase dataObject, bool allowUndo = true, bool notify = true, object notificationSender = null)
        {
            // Attempt to get a facade to write the change
            var facade = dataObject.GetFacade();
            if (facade == null)
                throw new Exception(string.Format("Failed to find facade to duplicate a {0}", dataObject.GetType().Name));

            DataObjectBase cloneDataObject = null;
            facade.ExecuteUnitOfWork(uow =>
            {
                uow.StartUiTracking(notificationSender, notify, allowUndo);

                // Create a clone of the data object in this session
                cloneDataObject = Clone(dataObject, uow, "Oid");
            });

            // Return the new data object
            return cloneDataObject;
        }

        /// <summary>
        /// Helper method to asynchronously duplicate a data object and write it immediately.
        /// This method will optionally store the change on the undo stack and notify widgets.
        /// </summary>
        /// <param name="dataObject">The data object being duplicated.</param>
        /// <param name="allowUndo">Indicates if the change should be recorded on the undo stack.</param>
        /// <param name="notify">Indicates if widgets should be notified about the change.</param>
        /// <param name="notificationSender">The sender to use when sending notifications.</param>
        /// <returns>The new duplicate data object.</returns>
        public static async Task<DataObjectBase> DuplicateDataObjectAsync(this DataObjectBase dataObject, bool allowUndo = true, bool notify = true, object notificationSender = null)
        {
            // Attempt to get a facade to write the change
            var facade = dataObject.GetFacade();
            if (facade == null)
                throw new Exception(string.Format("Failed to find facade to duplicate a {0}", dataObject.GetType().Name));

            DataObjectBase cloneDataObject = null;
            await facade.ExecuteUnitOfWorkAsync(uow =>
            {
                uow.StartUiTracking(notificationSender, notify, allowUndo);

                // Create a clone of the data object in this session
                cloneDataObject = Clone(dataObject, uow, "Oid");
            }).ConfigureAwait(false);

            // Return the new data object
            return cloneDataObject;
        }

        /// <summary>
        /// Creates a new data object in the specified session with values copied from this data object.
        /// </summary>
        /// <param name="dataObject">The data object to clone.</param>
        /// <param name="session">The session to create the data object in.</param>
        /// <param name="copyPersistentCollections">Indicates if the contents of persistent collections should be copied.</param>
        /// <param name="copyValues">Indicates if values should be copied from the source data object.</param>
        /// <returns>The new clone of the data object.</returns>
        public static DataObjectBase Clone(this DataObjectBase dataObject, Session session, bool copyValues = true, bool copyPersistentCollections = true)
        {
            // Create a new data object
            var newDataObject = DataObjectHelper.CreateDataObject(dataObject.GetType(), session);

            // Copy all properties from the original data object to the new data object
            if (copyValues)
                CopyTo(dataObject, newDataObject, false, copyPersistentCollections);

            // Return the new data object
            return newDataObject;
        }

        /// <summary>
        /// Creates a new data object in the specified session with values copied from this data object.
        /// </summary>
        /// <param name="dataObject">The data object to clone.</param>
        /// <param name="session">The session to create the data object in.</param>
        /// <param name="excludedProperties">An array of property names to exlude from the copy.</param>
        /// <returns>The new clone of the data object.</returns>
        public static DataObjectBase Clone(this DataObjectBase dataObject, Session session, params string[] excludedProperties)
        {
            // Create a new data object
            var newDataObject = DataObjectHelper.CreateDataObject(dataObject.GetType(), session);

            // Copy all properties from the original data object to the new data object
            CopyTo(dataObject, newDataObject, excludedProperties);

            // Return the new data object
            return newDataObject;
        }

        /// <summary>
        /// Copies all values from this data object to another.
        /// </summary>
        /// <param name="dataObject">The data object to copy values from.</param>
        /// <param name="targetDataObject">The data object to copy values to.</param>
        /// <param name="editorPropertiesOnly">
        /// If true, only properties that will be displayed in generated editors will be copied.
        /// if false, all properties will be copied.
        /// </param>
        /// <param name="copyPersistentCollections">Indicates if the contents of persistent collections should be copied.</param>
        public static void CopyTo(this DataObjectBase dataObject, DataObjectBase targetDataObject, bool editorPropertiesOnly = false, bool copyPersistentCollections = true)
        {
            // Abort if the source or target are null
            if (dataObject == null || targetDataObject == null)
                return;

            // Get the relevant properties
            var properties = editorPropertiesOnly
                ? dataObject.GetType().GetVisibleProperties()
                : dataObject.GetType().GetSupportedProperties(true);

            if (properties == null)
                return;

            // Copy the object
            CopyTo(dataObject, targetDataObject, properties, copyPersistentCollections);
        }

        /// <summary>
        /// Copies all values from this data object to another.
        /// </summary>
        /// <param name="dataObject">The data object to copy values from.</param>
        /// <param name="targetDataObject">The data object to copy values to.</param>
        /// <param name="excludedProperties">An array of property names to exlude from the copy.</param>
        public static void CopyTo(this DataObjectBase dataObject, DataObjectBase targetDataObject, params string[] excludedProperties)
        {
            // Abort if the source or target are null
            if (dataObject == null || targetDataObject == null)
                return;

            // Get the relevant properties
            var excludedPropertiesList = excludedProperties.ToList();
            var supportedProperties = dataObject.GetType().GetSupportedProperties(true);
            if (supportedProperties == null)
                return;

            var properties = supportedProperties.Where(p => !excludedPropertiesList.Contains(p.Name));

            // Copy the object
            CopyTo(dataObject, targetDataObject, properties);
        }

        /// <summary>
        /// Copies the specified properties from this data object to another.
        /// </summary>
        /// <param name="dataObject">The data object to copy values from.</param>
        /// <param name="targetDataObject">The data object to copy values to.</param>
        /// <param name="properties">A list of the properties to copy.</param>
        /// <param name="copyPersistentCollections">Indicates if the contents of persistent collections should be copied.</param>
        public static void CopyTo(this DataObjectBase dataObject, DataObjectBase targetDataObject, IEnumerable<IEdmPropertyInfo> properties, bool copyPersistentCollections = true)
        {
            // Abort if the source or target are null
            if (dataObject == null || targetDataObject == null)
                return;

            // Copy all properties from the source data object to the target data object
            foreach (var property in properties)
            {
                // Attempt to get the PropertyDescriptor for the property
                var propertyDescriptor = property.ContextObject as PropertyDescriptor;
                if (propertyDescriptor == null)
                    continue;

                // Skip the property if it has a DoNotCopyAttribute
                if (propertyDescriptor.Attributes.OfType<DoNotCopyAttribute>().Any())
                    continue;

                // If the property is XPBaseCollection then we need to copy the contents of the collection
                if (typeof(XPBaseCollection).IsAssignableFrom(property.PropertyType))
                {
                    // Don't copy the collection if requested to skip them
                    if (!copyPersistentCollections)
                        continue;

                    ((XPBaseCollection)propertyDescriptor.GetValue(targetDataObject)).SyncXpCollectionFrom((XPBaseCollection)propertyDescriptor.GetValue(dataObject));
                }
                else // Otherwise, just copy the property value
                {
                    // Ignore the property if it doesn't have a setter
                    if (propertyDescriptor.IsReadOnly)
                        continue;

                    // Collect the property value
                    var value = propertyDescriptor.GetValue(dataObject);

                    // If the value is a DataObjectBase, get a copy of it in the target session
                    if (value != null && typeof(DataObjectBase).IsAssignableFrom(property.PropertyType))
                        value = targetDataObject.Session.GetDataObject(value, value.GetType());

                    // Write the property value
                    targetDataObject.SetDataProperty(property.Name, value, false, false);
                }
            }
        }
    }
}
