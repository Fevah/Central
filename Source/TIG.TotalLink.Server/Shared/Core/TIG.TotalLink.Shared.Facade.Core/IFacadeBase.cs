using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.Facade.Core
{
    public interface IFacadeBase
    {
        #region Public Events

        event DataConnectedEventHandler DataConnected;
        event MethodConnectedEventHandler MethodConnected;

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if any of the services are connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Indicates if the data service is connected.
        /// </summary>
        bool IsDataConnected { get; }

        /// <summary>
        /// Indicates if the local data service is connected.
        /// </summary>
        bool IsLocalDataConnected { get; }

        /// <summary>
        /// Indicates if the method service is connected.
        /// </summary>
        bool IsMethodConnected { get; }

        /// <summary>
        /// The DataLayer from the DataFacade.
        /// </summary>
        IDataLayer DataLayer { get; }

        /// <summary>
        /// The LocalDataLayer from the LocalDataFacade.
        /// </summary>
        IDataLayer LocalDataLayer { get; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Connects to all services this facade provides.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        void Connect(ServiceTypes serviceType = ServiceTypes.All);

        /// <summary>
        /// Notifies the cache that the specified tables have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="dirtyTableNames">An array of names of the tables that have changed.</param>
        void NotifyDirtyTables(params string[] dirtyTableNames);

        /// <summary>
        /// Notifies the cache that the specified types have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="dirtyTypes">An array of types that have changed.</param>
        void NotifyDirtyTypes(params Type[] dirtyTypes);

        /// <summary>
        /// Notifies the cache that the specified types have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="changes">An array of changes containing the types that have changed.</param>
        void NotifyDirtyTypes(params EntityChange[] changes);

        /// <summary>
        /// Creates an XPInstantFeedbackSource for the specified entity type.
        /// </summary>
        /// <param name="entityType">The type of entity that the XPInstantFeedbackSource will collect.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An XPInstantFeedbackSource.</returns>
        XPInstantFeedbackSource CreateInstantFeedbackSource(Type entityType, ServiceTypes serviceType = ServiceTypes.Data);

        /// <summary>
        /// Creates an XPInstantFeedbackSource for the specified entity type.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <typeparam name="TEntity">The type of entity that the XPInstantFeedbackSource will collect.</typeparam>
        /// <returns>An XPInstantFeedbackSource.</returns>
        XPInstantFeedbackSource CreateInstantFeedbackSource<TEntity>(ServiceTypes serviceType = ServiceTypes.Data)
            where TEntity : DataObjectBase;

        /// <summary>
        /// Creates and returns a UnitOfWork.
        /// This UnitOfWork must be manually committed and/or disposed once you have finished using it.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>A new UnitOfWork.</returns>
        UnitOfWork CreateUnitOfWork(ServiceTypes serviceType = ServiceTypes.Data);

        /// <summary>
        /// Executes a UnitOfWork.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        void ExecuteUnitOfWork(Action<UnitOfWork> action, ServiceTypes serviceType = ServiceTypes.Data);

        /// <summary>
        /// Executes a UnitOfWork asynchronously.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        Task ExecuteUnitOfWorkAsync(Action<UnitOfWork> action, ServiceTypes serviceType = ServiceTypes.Data);

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        void ExecuteUnitOfWork(Func<UnitOfWork, bool> func, ServiceTypes serviceType = ServiceTypes.Data);

        /// <summary>
        /// Executes a UnitOfWork asynchronously on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        Task ExecuteUnitOfWorkAsync(Func<UnitOfWork, bool> func, ServiceTypes serviceType = ServiceTypes.Data);

        /// <summary>
        /// Executes a query.
        /// </summary>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        IEnumerable<TEntity> ExecuteQuery<TEntity>(Func<UnitOfWork, IQueryable<TEntity>> func, ServiceTypes serviceType = ServiceTypes.Data)
            where TEntity : DataObjectBase;

        /// <summary>
        /// Executes a query asynchronously.
        /// </summary>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        Task<IEnumerable<TEntity>> ExecuteQueryAsync<TEntity>(Func<UnitOfWork, IQueryable<TEntity>> func, ServiceTypes serviceType = ServiceTypes.Data)
            where TEntity : DataObjectBase;

        #endregion
    }
}
