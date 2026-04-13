using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Core
{
    #region Public Delegates

    public delegate void DataConnectedEventHandler(object sender, EventArgs e);
    public delegate void MethodConnectedEventHandler(object sender, EventArgs e);

    #endregion


    #region Public Enums

    [Flags]
    public enum ServiceTypes
    {
        Data = 1,
        LocalData = 2,
        Method = 4,
        All = Data | Method | LocalData
    }

    #endregion


    public abstract class FacadeBase<TDataEntity, TMethodContract> : IFacadeBase
        where TDataEntity : DataObjectBase
        where TMethodContract : IMethodServiceBase
    {
        #region Public Events

        public event DataConnectedEventHandler DataConnected;
        public event MethodConnectedEventHandler MethodConnected;

        #endregion


        #region Private Constants

        private const int BaseMethodPortOffset = 100;

        #endregion


        #region Private Fields

        private readonly IServiceConfiguration _serviceConfiguration;
        private readonly ILocalStoreConfiguration _localStoreConfiguration;
        private static bool _isConnecting;
        private static Task[] _connectionTasks;
        private Assembly[] _dataModelAssemblies;

        #endregion


        #region Protected Fields

        protected readonly FacadeAttribute FacadeAttribute;
        protected DataFacade<TDataEntity> DataFacade;
        protected MethodFacade<TMethodContract> MethodFacade;

        #endregion


        #region Constructors

        protected FacadeBase(IServiceConfiguration serviceConfiguration, params Assembly[] dataModelAssemblies)
        {
            // Store services
            _serviceConfiguration = serviceConfiguration;
            _dataModelAssemblies = dataModelAssemblies;

            // Get the FacadeAttribute attached to the derived class
            FacadeAttribute = (FacadeAttribute)GetType().GetCustomAttributes(typeof(FacadeAttribute), true).Single();
        }

        protected FacadeBase(IServiceConfiguration serviceConfiguration, ILocalStoreConfiguration localStoreConfiguration, params Assembly[] dataModelAssemblies)
            : this(serviceConfiguration, dataModelAssemblies)
        {
            // Store services
            _localStoreConfiguration = localStoreConfiguration;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Indicates if all of the services are connected.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return IsDataConnected
                    && (FacadeAttribute.HasMethodService && IsMethodConnected)
                    && (FacadeAttribute.HasLocalDataService && IsLocalDataConnected);
            }
        }

        /// <summary>
        /// Indicates if the data service is connected.
        /// </summary>
        public bool IsDataConnected
        {
            get { return DataFacade != null; }
        }

        /// <summary>
        /// Indicates if the Local data service is connected.
        /// </summary>
        public bool IsLocalDataConnected
        {
            get { return LocalDataFacade != null; }
        }


        /// <summary>
        /// Indicates if the method service is connected.
        /// </summary>
        public bool IsMethodConnected
        {
            get { return MethodFacade != null; }
        }

        /// <summary>
        /// The DataLayer from the DataFacade.
        /// </summary>
        public IDataLayer DataLayer
        {
            get { return DataFacade != null ? DataFacade.DataLayer : null; }
        }

        /// <summary>
        /// The LocalDataFacade for service to local data store.
        /// </summary>
        public LocalDataFacade<TDataEntity> LocalDataFacade { get; private set; }

        /// <summary>
        /// The DataLayer from the LocalDataFacade.
        /// </summary>
        public IDataLayer LocalDataLayer
        {
            get { return LocalDataFacade != null ? LocalDataFacade.DataLayer : null; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Connects to all services this facade provides.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        public void Connect(ServiceTypes serviceType = ServiceTypes.All)
        {
            if (!_isConnecting)
            {
                // If we are not already trying to connect, start connecting
                _connectionTasks = null;
                _isConnecting = true;

                // Start connecting to all specified services
                var connectionTasks = new List<Task>();
                if (!IsDataConnected && (serviceType & ServiceTypes.Data) == ServiceTypes.Data)
                    connectionTasks.Add(Task.Run(() => ConnectDataService(_serviceConfiguration.Server, _serviceConfiguration.BasePort, _serviceConfiguration.AuthenticationToken)));
                if (!IsMethodConnected && (serviceType & ServiceTypes.Method) == ServiceTypes.Method)
                    connectionTasks.Add(Task.Run(() => ConnectMethodService(_serviceConfiguration.Server, _serviceConfiguration.BasePort, _serviceConfiguration.AuthenticationToken)));
                if (!IsLocalDataConnected && _localStoreConfiguration != null && (serviceType & ServiceTypes.LocalData) == ServiceTypes.LocalData)
                    connectionTasks.Add(Task.Run(() => ConnectLocalDataService(_localStoreConfiguration.GetConnection())));

                _connectionTasks = connectionTasks.ToArray();

                try
                {
                    if (_connectionTasks.Length > 0)
                    {
                        // Wait for the connection tasks to complete
                        Task.WaitAll(_connectionTasks);

                        // Raise connected events
                        if (IsDataConnected)
                            RaiseDataConnected();
                        if (IsMethodConnected)
                            RaiseMethodConnected();
                    }
                }
                finally
                {
                    // Connection has completed
                    _isConnecting = false;
                }
            }
            else
            {
                // Wait until all connection tasks are set
                while (_isConnecting && _connectionTasks == null)
                {
                    Task.Delay(100);
                }

                if (_connectionTasks != null && _connectionTasks.Length > 0)
                {
                    // Wait for the connection tasks to complete
                    Task.WaitAll(_connectionTasks);
                }
            }
        }

        /// <summary>
        /// Notifies the cache that the specified tables have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="dirtyTableNames">An array of names of the tables that have changed.</param>
        public void NotifyDirtyTables(params string[] dirtyTableNames)
        {
            if (!IsDataConnected)
                return;

            DataFacade.NotifyDirtyTables(dirtyTableNames);
        }

        /// <summary>
        /// Notifies the cache that the specified types have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="dirtyTypes">An array of types that have changed.</param>
        public void NotifyDirtyTypes(params Type[] dirtyTypes)
        {
            if (!IsDataConnected)
                return;

            DataFacade.NotifyDirtyTypes(dirtyTypes);
        }

        /// <summary>
        /// Notifies the cache that the specified types have changed, and therefore they should be retrieved from the server when next requested.
        /// </summary>
        /// <param name="changes">An array of changes containing the types that have changed.</param>
        public void NotifyDirtyTypes(params EntityChange[] changes)
        {
            if (!IsDataConnected)
                return;

            DataFacade.NotifyDirtyTypes(changes.Select(c => c.EntityType).Distinct().ToArray());
        }

        /// <summary>
        /// Creates an XPInstantFeedbackSource for the specified entity type.
        /// </summary>
        /// <param name="entityType">The type of entity that the XPInstantFeedbackSource will collect.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An XPInstantFeedbackSource.</returns>
        public XPInstantFeedbackSource CreateInstantFeedbackSource(Type entityType, ServiceTypes serviceType = ServiceTypes.Data)
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return null;

                return DataFacade.CreateInstantFeedbackSource(entityType);
            }

            if (!IsLocalDataConnected)
                return null;

            return LocalDataFacade.CreateInstantFeedbackSource(entityType);

        }

        /// <summary>
        /// Creates an XPInstantFeedbackSource for the specified entity type.
        /// </summary>
        /// <typeparam name="TEntity">The type of entity that the XPInstantFeedbackSource will collect.</typeparam>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An XPInstantFeedbackSource.</returns>
        public XPInstantFeedbackSource CreateInstantFeedbackSource<TEntity>(ServiceTypes serviceType = ServiceTypes.Data)
            where TEntity : DataObjectBase
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return null;

                return DataFacade.CreateInstantFeedbackSource<TEntity>();
            }

            if (!IsLocalDataConnected)
                return null;

            return LocalDataFacade.CreateInstantFeedbackSource<TEntity>();
        }

        /// <summary>
        /// Creates and returns a UnitOfWork.
        /// This UnitOfWork must be manually committed and/or disposed once you have finished using it.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>A new UnitOfWork.</returns>
        public UnitOfWork CreateUnitOfWork(ServiceTypes serviceType = ServiceTypes.Data)
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return null;

                return DataFacade.CreateUnitOfWork();
            }

            return LocalDataFacade.CreateUnitOfWork();
        }

        /// <summary>
        /// Executes a UnitOfWork.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public void ExecuteUnitOfWork(Action<UnitOfWork> action, ServiceTypes serviceType = ServiceTypes.Data)
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return;

                DataFacade.ExecuteUnitOfWork(action);
                return;
            }

            LocalDataFacade.ExecuteUnitOfWork(action);
        }

        /// <summary>
        /// Executes a UnitOfWork asynchronously.
        /// </summary>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <param name="action">The work to perform within the UnitOfWork.</param>
        public async Task ExecuteUnitOfWorkAsync(Action<UnitOfWork> action, ServiceTypes serviceType = ServiceTypes.Data)
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return;

                await DataFacade.ExecuteUnitOfWorkAsync(action).ConfigureAwait(false);
                return;
            }

            await LocalDataFacade.ExecuteUnitOfWorkAsync(action).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a UnitOfWork on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        public void ExecuteUnitOfWork(Func<UnitOfWork, bool> func, ServiceTypes serviceType = ServiceTypes.Data)
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return;

                DataFacade.ExecuteUnitOfWork(func);
                return;
            }

            LocalDataFacade.ExecuteUnitOfWork(func);
        }

        /// <summary>
        /// Executes a UnitOfWork asynchronously on the specified data layer and optionally commits it.
        /// </summary>
        /// <param name="func">The work to perform within the UnitOfWork.  This function must return true to commit, or false to rollback.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        public async Task ExecuteUnitOfWorkAsync(Func<UnitOfWork, bool> func, ServiceTypes serviceType = ServiceTypes.Data)
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return;

                await DataFacade.ExecuteUnitOfWorkAsync(func).ConfigureAwait(false);
                return;
            }

            await LocalDataFacade.ExecuteUnitOfWorkAsync(func).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a query.
        /// </summary>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        public IEnumerable<TEntity> ExecuteQuery<TEntity>(Func<UnitOfWork, IQueryable<TEntity>> func, ServiceTypes serviceType = ServiceTypes.Data)
            where TEntity : DataObjectBase
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return null;

                return DataFacade.ExecuteQuery(func);
            }

            return LocalDataFacade.ExecuteQuery(func);
        }

        /// <summary>
        /// Executes a query asynchronously.
        /// </summary>
        /// <param name="func">A function which returns the query to execute.</param>
        /// <param name="serviceType">The type of services to connect to.</param>
        /// <returns>An IEnumerable containing the result of the query.</returns>
        public async Task<IEnumerable<TEntity>> ExecuteQueryAsync<TEntity>(Func<UnitOfWork, IQueryable<TEntity>> func, ServiceTypes serviceType = ServiceTypes.Data)
            where TEntity : DataObjectBase
        {
            if (serviceType == ServiceTypes.Data)
            {
                if (!IsDataConnected)
                    return null;

                return await DataFacade.ExecuteQueryAsync(func).ConfigureAwait(false);
            }

            return await LocalDataFacade.ExecuteQueryAsync(func).ConfigureAwait(false);
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when the data facade has connected.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnDataConnected(EventArgs e)
        {
            if (DataConnected != null)
                DataConnected(this, e);
        }

        /// <summary>
        /// Called when the method facade has connected.
        /// </summary>
        /// <param name="e">Event arguments.</param>
        protected virtual void OnMethodConnected(EventArgs e)
        {
            if (MethodConnected != null)
                MethodConnected(this, e);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Raises the DataConnected event.
        /// </summary>
        private void RaiseDataConnected()
        {
            OnDataConnected(new EventArgs());
        }

        /// <summary>
        /// Raises the MethodConnected event.
        /// </summary>
        private void RaiseMethodConnected()
        {
            OnMethodConnected(new EventArgs());
        }


        private void ConnectLocalDataService(string connection)
        {
            // Abort if the data facade is already connected
            if (LocalDataFacade != null)
                return;

            if (!FacadeAttribute.HasLocalDataService)
                return;

            // Create a LocalDataFacade
            try
            {
                LocalDataFacade = new LocalDataFacade<TDataEntity>(connection);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Failed to connect to local data service! \n {0}", ex.Message));
            }
        }

        /// <summary>
        /// Connects to the data service this facade provides.
        /// </summary>
        /// <param name="server">Name of the server to connect to.</param>
        /// <param name="basePort">Base service port.</param>
        /// <param name="authenticationToken">authentication token</param>
        private void ConnectDataService(string server, int basePort, string authenticationToken)
        {
            // Abort if the data facade is already connected
            if (DataFacade != null)
                return;

            if (!FacadeAttribute.HasDataService)
                return;

            // Create a DataFacade
            var dataAddress = string.Format("http://{0}:{1}/{2}DataService.svc/", server,
                basePort + FacadeAttribute.DataPortOffset, FacadeAttribute.DataServiceName);
            try
            {
                DataFacade = new DataFacade<TDataEntity>(dataAddress, authenticationToken, _dataModelAssemblies);
            }
            catch (EndpointNotFoundException ex)
            {
                throw new Exception(string.Format("Failed to connect to data service at '{0}'!", dataAddress));
            }
        }

        /// <summary>
        /// Connects to the data service this facade provides.
        /// </summary>
        /// <param name="server">Name of the server to connect to.</param>
        /// <param name="basePort">Base service port.</param>
        /// <param name="authenticationToken">authentication token</param>
        private void ConnectMethodService(string server, int basePort, string authenticationToken)
        {
            // Abort if the method facade is already connected
            if (MethodFacade != null)
                return;

            // If details have been specified for a method service, then create a MethodFacade
            if (!FacadeAttribute.HasMethodService)
                return;

            var methodAddress = string.Format("http://{0}:{1}/{2}MethodService.svc/", server, basePort + BaseMethodPortOffset + FacadeAttribute.MethodPortOffset, FacadeAttribute.MethodServiceName);
            try
            {
                MethodFacade = new MethodFacade<TMethodContract>(methodAddress, authenticationToken);
            }
            catch (EndpointNotFoundException ex)
            {
                throw new Exception(string.Format("Failed to connect to method service at '{0}'!", methodAddress));
            }
        }

        #endregion
    }
}
