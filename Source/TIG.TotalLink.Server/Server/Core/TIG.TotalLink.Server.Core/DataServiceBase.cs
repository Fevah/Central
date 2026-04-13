using System;
using System.Collections.Generic;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Helpers;
using Newtonsoft.Json.Linq;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;

namespace TIG.TotalLink.Server.Core
{
    public abstract class DataServiceBase : CachedDataStoreService
    {
        #region Private Fields

        public static IGlobalFacade _globalFacade;

        #endregion


        #region Public Fields

        public static ICachedDataStore CacheRoot;
        public static IDataLayer LocalDataLayer;

        #endregion


        #region Constructors

        protected DataServiceBase(ICachedDataStore dataStore)
            : base(dataStore)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets a data layer which connects to the local DataCacheRoot.
        /// Use this when you want to access data that is provided by this service.
        /// If you wish to access another service, create an appropriate facade instead.
        /// </summary>
        /// <typeparam name="TPrimaryEntity">The primary entity in the required data model.</typeparam>
        /// <returns>A ThreadSafeDataLayer.</returns>
        public IDataLayer GetLocalDataLayer<TPrimaryEntity>()
            where TPrimaryEntity : DataObjectBase
        {
            // If a local data layer has already been created, return the existing one
            if (LocalDataLayer != null)
                return LocalDataLayer;

            // Create the local data layer
            var dict = DataModelHelper.GetReflectionDictionary(typeof(TPrimaryEntity).Assembly, typeof(XPObjectType).Assembly);
            var cacheNode = new DataCacheNodeLocal(CacheRoot);
            LocalDataLayer = new ThreadSafeDataLayer(dict, cacheNode);

            // Return the local data layer
            return LocalDataLayer;
        }

        /// <summary>
        /// Executes a UnitOfWork on the local data layer.
        /// </summary>
        /// <typeparam name="TPrimaryEntity">The primary entity in the required data model.</typeparam>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public void ExecuteLocalUnitOfWork<TPrimaryEntity>(Action<UnitOfWork> action)
            where TPrimaryEntity : DataObjectBase
        {
            var dataLayer = GetLocalDataLayer<TPrimaryEntity>();
            FacadeHelper.ExecuteUnitOfWork(dataLayer, action);
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Build Entity from cached service query statement
        /// </summary>
        /// <typeparam name="T">Type of result entity</typeparam>
        /// <param name="parameters">Parameters of query</param>
        /// <param name="operands">Opreator information</param>
        /// <returns>Entity</returns>
        protected T BuildQueryEntity<T>(object[] parameters, IEnumerable<QueryOperand> operands) where T : DataObjectBase
        {
            var index = 0;
            var jEntity = new JObject();
            foreach (var operand in operands)
            {
                // Ignore extra object.
                if (operand.ColumnType == DBColumnType.Guid)
                {
                    index++;
                    continue;
                }

                var name = operand.ColumnName;
                var value = parameters[index];
                jEntity[name] = new JValue(value);
                index++;
            }

            return jEntity.ToObject<T>();
        }

        /// <summary>
        /// Creates a DataCacheRoot using the supplied data store.
        /// </summary>
        /// <param name="dataStore">The data store that the DataCacheRoot should collect data from.</param>
        /// <returns>The DataCacheRoot.</returns>
        protected static ICachedDataStore CreateCacheRoot(IDataStore dataStore)
        {
            // If the DataCacheRoot has already been created, return the existing one
            if (CacheRoot != null)
                return CacheRoot;

            // Create a new DataCacheRoot based on the supplied data store
            var dataCacheRoot = new DataCacheRoot(dataStore);
            dataCacheRoot.Configure(new DataCacheConfiguration(DataCacheConfigurationCaching.All));
            CacheRoot = dataCacheRoot;

            // Return the DataCacheRoot
            return CacheRoot;
        }

        /// <summary>
        /// Creates a persistent data store for the specified DatabaseDomain.
        /// </summary>
        /// <param name="databaseDomain">The DatabaseDomain to get the data store for.</param>
        protected static IDataStore CreateDatabaseDataStore(DatabaseDomain databaseDomain)
        {
            // Get the GlobalFacade
            var globalFacade = GetGlobalMethodFacade();

            // Get the connection string from the GlobalFacade
            var connectionString = globalFacade.GetConnectionString(databaseDomain);

            // Create a data store
            var dataStore = XpoDefault.GetConnectionProvider(connectionString, AutoCreateOption.SchemaAlreadyExists);

            // Return the data store
            return dataStore;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes and returns a GlobalFacade connected to the method service only.
        /// </summary>
        /// <returns>A GlobalFacade.</returns>
        private static IGlobalFacade GetGlobalMethodFacade()
        {
            if (_globalFacade == null)
                _globalFacade = new GlobalFacade(new ServerServiceConfiguration());

            if (_globalFacade != null && !_globalFacade.IsMethodConnected)
                _globalFacade.Connect(ServiceTypes.Method);

            return _globalFacade;
        }

        #endregion
    }
}
