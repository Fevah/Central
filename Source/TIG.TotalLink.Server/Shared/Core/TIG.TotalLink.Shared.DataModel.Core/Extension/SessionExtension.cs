using System;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Core.Extension
{
    public static class SessionExtension
    {
        #region Public Methods

        /// <summary>
        /// Gets a copy of the specified object in this Session.
        /// </summary>
        /// <typeparam name="T">The type of object being collected.</typeparam>
        /// <param name="session">The Session to get the object in.</param>
        /// <param name="obj">The object to collect.</param>
        /// <returns>A copy of the object from within this Session.</returns>
        public static T GetDataObject<T>(this Session session, T obj)
            where T : DataObjectBase
        {
            return (obj != null ? session.GetObjectByKey<T>(obj.Oid) : null);
        }

        /// <summary>
        /// Gets a copy of the specified object in this Session.
        /// </summary>
        /// <param name="session">The Session to get the object in.</param>
        /// <param name="obj">The object to collect.</param>
        /// <param name="type">The type of object being collected.</param>
        /// <returns>A copy of the object from within this Session.</returns>
        public static DataObjectBase GetDataObject(this Session session, object obj, Type type)
        {
            var dataObject = DataModelHelper.GetDataObject(obj);
            return (dataObject != null ? session.GetObjectByKey(type, dataObject.Oid) as DataObjectBase : null);
        }

        /// <summary>
        /// Gets a copy of the specified object in this Session.
        /// </summary>
        /// <param name="session">The Session to get the object in.</param>
        /// <param name="obj">The object to collect.</param>
        /// <returns>A copy of the object from within this Session.</returns>
        public static DataObjectBase GetDataObject(this Session session, object obj)
        {
            var dataObject = DataModelHelper.GetDataObject(obj);
            return (dataObject != null ? session.GetObjectByKey(dataObject.GetType(), dataObject.Oid) as DataObjectBase : null);
        }

        /// <summary>
        /// Executes a NestedUnitOfWork within the specified session.
        /// </summary>
        /// <param name="session">The session to create the NestedUnitOfWork within.</param>
        /// <param name="func">The work to perform within the NestedUnitOfWork.  This function must return true to commit, or false to rollback.</param>
        /// <param name="completeAction">An action to execute after the NestedUnitOfWork has been committed.  This action will not be called if <paramref name="func"/> return false.</param>
        public static void ExecuteNestedUnitOfWork(this Session session, Func<NestedUnitOfWork, bool> func, Action<NestedUnitOfWork> completeAction = null)
        {
            // Start the NestedUnitOfWork
            using (var nuow = session.BeginNestedUnitOfWork())
            {
                // Execute the supplied function
                if (func(nuow))
                {
                    // If the function returned true, commit the NestedUnitOfWork
                    nuow.CommitChanges();

                    // Execute the complete action, if there is one
                    if (completeAction != null)
                        completeAction(nuow);
                }
            }
        }

        #endregion
    }
}
