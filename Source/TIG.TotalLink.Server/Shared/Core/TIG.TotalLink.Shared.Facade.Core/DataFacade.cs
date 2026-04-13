using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Xml;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Metadata;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.DataSource;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientBehavior;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientMessageInspector;

namespace TIG.TotalLink.Shared.Facade.Core
{
    public class DataFacade<TPrimaryEntity>
        where TPrimaryEntity : DataObjectBase
    {
        #region Private Fields

        private DataCacheNode _cacheNode;

        #endregion


        #region Constructors

        public DataFacade(string address, string authenticationToken, params Assembly[] dataModelAssemblies)
        {
            // Create an endpoint and binding for the service
            var endpoint = new EndpointAddress(address);
            var binding = new BasicHttpBinding
            {
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                TransferMode = TransferMode.Streamed,
                OpenTimeout = new TimeSpan(0, 5, 0),
                CloseTimeout = new TimeSpan(0, 5, 0),
                SendTimeout = new TimeSpan(0, 5, 0),
                ReceiveTimeout = new TimeSpan(0, 5, 0),
                ReaderQuotas = new XmlDictionaryReaderQuotas()
                {
                    MaxDepth = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                }
            };

            // Prepare a list of data model assemblies
            var assemblies = new List<Assembly>
            {
                typeof(TPrimaryEntity).Assembly
            };
            if (dataModelAssemblies != null)
                assemblies.AddRange(dataModelAssemblies);

            // Create a cached client
            var dict = DataModelHelper.GetReflectionDictionary(assemblies.ToArray());
            var client = new CachedDataStoreClient(binding, endpoint);

            //// Create a cached client
            //var dict = DataModelHelper.GetReflectionDictionary(typeof(TPrimaryEntity).Assembly);
            //var client = new CachedDataStoreClient(binding, endpoint);

            // If an authentication token was specified, add an authentication inspector to the channel
            if (!string.IsNullOrEmpty(authenticationToken))
            {
                var inspector = new AuthenticationClientMessageInspector(authenticationToken);
                client.Endpoint.EndpointBehaviors.Add(new AuthenticationEndpointClientBehavior(inspector));
            }

            // Create a cached data layer
            _cacheNode = new DataCacheNode(client);
            DataLayer = new ThreadSafeDataLayer(dict, _cacheNode);
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
        /// Notifies the cache that the specified tables have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="dirtyTableNames">An array of names of the tables that have changed.</param>
        public void NotifyDirtyTables(params string[] dirtyTableNames)
        {
            if (DataLayer == null)
                return;

            _cacheNode.NotifyDirtyTables(dirtyTableNames);
        }

        /// <summary>
        /// Notifies the cache that the specified types have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="dirtyTypes">An array of types that have changed.</param>
        public void NotifyDirtyTypes(params Type[] dirtyTypes)
        {
            if (DataLayer == null)
                return;

            var dirtyTableNames = DataLayer.Dictionary.Classes.Cast<XPClassInfo>()
                .Where(c => dirtyTypes.Contains(c.ClassType))
                .Select(c => c.TableName)
                .ToArray();
            if (dirtyTableNames.Length == 0)
                return;

            _cacheNode.NotifyDirtyTables(dirtyTableNames);
        }

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
