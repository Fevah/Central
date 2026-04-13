using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration
{
    public class Configuration : SyncEngineComponent, IConfiguration
    {
        #region Private Properties

        private readonly IIntegrationFacade _facade;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with required components.
        /// </summary>
        /// <param name="log"></param>
        /// <param name="facade"></param>
        public Configuration(ILog log, IIntegrationFacade facade)
            : base(log)
        {
            _facade = facade;
            _facade.Connect();
        }

        #endregion


        #region IConfiguration Members

        #region Settings

        public int ConcurrentSyncTaskLimit
        {
            get
            {
                RunDisposedCheck();

                var value = IntegrationServiceSection.Instance.ConcurrentSettings.SyncTasksLimit;
                return value;
            }
        }

        public int ConcurrentSyncInstanceBundleTasksPerSyncEntityBundleTaskLimit
        {
            get
            {
                RunDisposedCheck();

                var value = IntegrationServiceSection.Instance.ConcurrentSettings.SyncInstanceBundleTasksPerSyncEntityBundleTaskLimit;
                return value;
            }
        }

        public int ConcurrentSyncInstanceTasksPerSyncInstanceBundleTaskLimit
        {
            get
            {
                RunDisposedCheck();

                var value = IntegrationServiceSection.Instance.ConcurrentSettings.SyncInstanceTasksPerSyncInstanceBundleTaskLimit;
                return value;
            }
        }

        public TimeSpan ActiveSyncTasksPollingTimeout
        {
            get
            {
                RunDisposedCheck();

                var value = IntegrationServiceSection.Instance.SyncTaskSettings.ActiveSyncTasksPollingTimeout;
                return new TimeSpan(0, 0, 0, 0, value);
            }
        }

        public TimeSpan DefaultSyncTaskTimeout
        {
            get
            {
                RunDisposedCheck();

                var value = IntegrationServiceSection.Instance.SyncTaskSettings.DefaultSyncTaskTimeout;
                return new TimeSpan(0, 0, 0, 0, value);
            }
        }

        #endregion


        #region Safe Operation

        /// <summary>
        /// Get all active sync entity bundles
        /// </summary>
        /// <returns>Ids of active sync entity bundles</returns>
        public IEnumerable<Guid> GetIdsOfActiveSyncEntityBundles()
        {
            RunDisposedCheck();

            try
            {
                var allSyncTypes = _facade.ExecuteQuery(uow => uow.Query<SyncEntityBundle>().Where(i => i.IsActive == true));
                var ids = allSyncTypes.Select(i => i.Oid).ToArray();
                return ids;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read sync types.", ex);
                return new Guid[0];
            }
        }

        /// <summary>
        /// Check active sync task exists or not
        /// </summary>
        /// <param name="syncEntityBundleId">Sync entity bundle id</param>
        /// <returns>True, indicate active sync task exists</returns>
        public bool IsActiveSyncTaskExists(Guid syncEntityBundleId)
        {
            RunDisposedCheck();

            SyncEntityBundle syncEntityBundle;

            try
            {
                syncEntityBundle = _facade.ExecuteQuery(uow => uow.Query<SyncEntityBundle>().Where(i => i.Oid == syncEntityBundleId))
                    .SingleOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read sync type by id", ex);
                syncEntityBundle = null;
            }

            if (syncEntityBundle == null)
            {
                return false;
            }

            return syncEntityBundle.IsActive.HasValue && syncEntityBundle.IsActive.Value;
        }

        /// <summary>
        /// Get sync task timeout.
        /// </summary>
        /// <param name="syncEntityBundleId">Sync entity bundle id</param>
        /// <returns>Time span to check task is timeout or not.</returns>
        public TimeSpan GetSyncTaskTimeout(Guid syncEntityBundleId)
        {
            RunDisposedCheck();

            SyncEntityBundle syncEntityBundle;

            try
            {
                syncEntityBundle = _facade.ExecuteQuery(uow => uow.Query<SyncEntityBundle>().Where(i => i.Oid == syncEntityBundleId))
                    .SingleOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read sync type by id", ex);
                syncEntityBundle = null;
            }

            TimeSpan timeout;

            if (syncEntityBundle == null)
            {
                timeout = DefaultSyncTaskTimeout;
            }
            else if (syncEntityBundle.Timeout.HasValue)
            {
                timeout = new TimeSpan(0, 0, 0, 0, syncEntityBundle.Timeout.Value);
            }
            else
            {
                timeout = DefaultSyncTaskTimeout;
            }

            return timeout;
        }

        #endregion

        /// <summary>
        /// Get task data by entity bundle id
        /// </summary>
        /// <param name="entityBundleId">Sync entity bundle id</param>
        /// <returns>Task context</returns>
        public ITaskData GetTaskData(Guid entityBundleId)
        {
            var data = new TaskData(Log, entityBundleId, _facade);
            return data;
        }

        #endregion
    }
}
