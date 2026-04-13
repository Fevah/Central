using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Extension;

namespace TIG.TotalLink.Shared.Facade.Core.Helper
{
    public class FacadeHelper
    {
        #region Public Methods

        /// <summary>
        /// Creates and returns a UnitOfWork on the specified data layer.
        /// This UnitOfWork must be manually committed and/or disposed once you have finished using it.
        /// </summary>
        /// <param name="dataLayer">The data layer to create the UnitOfWork on.</param>
        /// <returns>A new UnitOfWork.</returns>
        public static UnitOfWork CreateUnitOfWork(IDataLayer dataLayer)
        {
            return new UnitOfWork(dataLayer);
        }

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and commits it.
        /// </summary>
        /// <param name="dataLayer">The data layer to execute the UnitOfWork on.</param>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public static void ExecuteUnitOfWork(IDataLayer dataLayer, Action<UnitOfWork> action)
        {
            // Start the UnitOfWork
            using (var uow = new UnitOfWork(dataLayer))
            {
                // Execute the supplied action
                action(uow);

                // Commit the UnitOfWork
                uow.CommitChanges();
            }
        }

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and commits it asynchronously.
        /// </summary>
        /// <param name="dataLayer">The data layer to execute the UnitOfWork on.</param>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public static async Task ExecuteUnitOfWorkAsync(IDataLayer dataLayer, Action<UnitOfWork> action)
        {
            // Start the UnitOfWork
            using (var uow = new UnitOfWork(dataLayer))
            {
                // Execute the supplied action
                action(uow);

                // Asynchronously commit the UnitOfWork
                await uow.CommitChangesAsync();
            }
        }

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="dataLayer">The data layer to execute the UnitOfWork on.</param>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        public static void ExecuteUnitOfWork(IDataLayer dataLayer, Func<UnitOfWork, bool> func)
        {
            // Start the UnitOfWork
            using (var uow = new UnitOfWork(dataLayer))
            {
                // Execute the supplied function
                if (func(uow))
                {
                    // If the function returned true, commit the UnitOfWork
                    uow.CommitChanges();
                }
            }
        }

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and optionally commits it asynchronously.
        /// </summary>
        /// <param name="dataLayer">The data layer to execute the UnitOfWork on.</param>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        public static async Task ExecuteUnitOfWorkAsync(IDataLayer dataLayer, Func<UnitOfWork, bool> func)
        {
            // Start the UnitOfWork
            using (var uow = new UnitOfWork(dataLayer))
            {
                // Execute the supplied function
                if (func(uow))
                {
                    // If the function returned true, asynchronously commit the UnitOfWork
                    await uow.CommitChangesAsync();
                }
            }
        }

        /// <summary>
        /// Executes a query on the specified data layer.
        /// </summary>
        /// <param name="dataLayer">The data layer to execute the UnitOfWork on.</param>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        public static IEnumerable<TEntity> ExecuteQuery<TEntity>(IDataLayer dataLayer, Func<UnitOfWork, IQueryable<TEntity>> func)
            where TEntity : DataObjectBase
        {
            // Start the UnitOfWork
            using (var uow = new UnitOfWork(dataLayer))
            {
                // Call the supplied function to create a query on the UnitOfWork
                var query = func(uow);

                // Return the result of the query
                return query.ToList();
            }
        }

        /// <summary>
        /// Executes a query asynchronously on the specified data layer.
        /// </summary>
        /// <param name="dataLayer">The data layer to execute the UnitOfWork on.</param>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        public static Task<IEnumerable<TEntity>> ExecuteQueryAsync<TEntity>(IDataLayer dataLayer, Func<UnitOfWork, IQueryable<TEntity>> func)
            where TEntity : DataObjectBase
        {
            // Create a TaskCompletionSource to control the async query
            var tcs = new TaskCompletionSource<IEnumerable<TEntity>>();

            try
            {
                // Start a UnitOfWork
                var uow = new UnitOfWork(dataLayer);

                // Call the supplied function to create a query on the UnitOfWork
                var query = func(uow);

                // Asynchronously enumerate the query
                query.EnumerateAsync(delegate(IEnumerable<TEntity> result, Exception ex)
                {
                    // Handle the result of the query and set the task result or exception as necessary
                    if (ex == null)
                        tcs.SetResult(result);
                    else
                        tcs.SetException(ex);

                    // Dispose the UnitOfWork
                    try
                    {
                        uow.Dispose();
                    }
                    catch
                    {
                        // Ignore dispose errors
                    }
                });
            }
            catch (Exception ex)
            {
                // Set the task exception
                tcs.SetException(ex);
            }

            // Return the task
            return tcs.Task;
        }

        #endregion
    }
}
