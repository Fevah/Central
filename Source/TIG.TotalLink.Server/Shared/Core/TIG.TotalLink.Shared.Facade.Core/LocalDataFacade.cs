using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.DataSource;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Shared.Facade.Core
{
    public class LocalDataFacade<TPrimaryEntity>
        where TPrimaryEntity : DataObjectBase
    {
        #region Constructors

        public LocalDataFacade(string connection)
        {
            var dict = DataModelHelper.GetReflectionDictionary(typeof(TPrimaryEntity).Assembly);
            // Create a Data Store
            var dataStore = XpoDefault.GetConnectionProvider(connection, AutoCreateOption.SchemaAlreadyExists);
            // Create a Data Layer
            DataLayer = new SimpleDataLayer(dict, dataStore);
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The DataLayer that this facade uses to collect data.
        /// </summary>
        public IDataLayer DataLayer { get; private set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Creates an XPInstantFeedbackSource for the specified entity type.
        /// </summary>
        /// <param name="entityType">The type of entity that the XPInstantFeedbackSource will collect.</param>
        /// <returns>An XPInstantFeedbackSource.</returns>
        public XPInstantFeedbackSource CreateInstantFeedbackSource(Type entityType)
        {
            return new XPInstantFeedbackSourceEx(DataLayer, entityType);
        }

        /// <summary>
        /// Creates an XPInstantFeedbackSource for the specified entity type.
        /// </summary>
        /// <typeparam name="TEntity">The type of entity that the XPInstantFeedbackSource will collect.</typeparam>
        /// <returns>An XPInstantFeedbackSource.</returns>
        public XPInstantFeedbackSource CreateInstantFeedbackSource<TEntity>()
            where TEntity : DataObjectBase
        {
            return CreateInstantFeedbackSource(typeof(TEntity));
        }

        /// <summary>
        /// Creates and returns a UnitOfWork.
        /// This UnitOfWork must be manually committed and/or disposed once you have finished using it.
        /// </summary>
        /// <returns>A new UnitOfWork.</returns>
        public UnitOfWork CreateUnitOfWork()
        {
            return FacadeHelper.CreateUnitOfWork(DataLayer);
        }

        /// <summary>
        /// Executes a UnitOfWork.
        /// </summary>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public void ExecuteUnitOfWork(Action<UnitOfWork> action)
        {
            FacadeHelper.ExecuteUnitOfWork(DataLayer, action);
        }

        /// <summary>
        /// Executes a UnitOfWork asynchronously.
        /// </summary>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public async Task ExecuteUnitOfWorkAsync(Action<UnitOfWork> action)
        {
            await FacadeHelper.ExecuteUnitOfWorkAsync(DataLayer, action).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        public void ExecuteUnitOfWork(Func<UnitOfWork, bool> func)
        {
            FacadeHelper.ExecuteUnitOfWork(DataLayer, func);
        }

        /// <summary>
        /// Executes a UnitOfWork asynchronously on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        public async Task ExecuteUnitOfWorkAsync(Func<UnitOfWork, bool> func)
        {
            await FacadeHelper.ExecuteUnitOfWorkAsync(DataLayer, func).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a query.
        /// </summary>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        public IEnumerable<TEntity> ExecuteQuery<TEntity>(Func<UnitOfWork, IQueryable<TEntity>> func)
            where TEntity : DataObjectBase
        {
            return FacadeHelper.ExecuteQuery(DataLayer, func);
        }

        /// <summary>
        /// Executes a query asynchronously.
        /// </summary>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        public async Task<IEnumerable<TEntity>> ExecuteQueryAsync<TEntity>(Func<UnitOfWork, IQueryable<TEntity>> func)
            where TEntity : DataObjectBase
        {
            return await FacadeHelper.ExecuteQueryAsync(DataLayer, func).ConfigureAwait(false);
        }

        #endregion
    }
}