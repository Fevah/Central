using System;
using TIG.IntegrationServer.SyncEngine.Core;
using TIG.IntegrationServer.SyncEngine.Core.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.SyncStatus
{
    public class SyncStatusManager : ISyncStatusManager
    {
        #region Private Properties

        private readonly ISyncStatusRepository _repository;

        #endregion


        #region Default Constructors

        /// <summary>
        /// Constructor with repository
        /// </summary>
        /// <param name="repository">Sync status repository</param>
        public SyncStatusManager(ISyncStatusRepository repository)
        {
            _repository = repository;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Get last sync entity time
        /// </summary>
        /// <param name="entityKey">Sync entity key</param>
        /// <param name="sourceAgentId">Source agent id</param>
        /// <param name="targetAgentId">Target agent id</param>
        /// <returns>Last sync entity time</returns>
        public DateTime? GetLastSyncTime(object entityKey, Guid? sourceAgentId, Guid? targetAgentId)
        {
            return _repository.GetLastSyncTime(entityKey.ToString(), sourceAgentId.ToString(),
                targetAgentId.ToString());
        }

        /// <summary>
        /// Set sync entity status
        /// </summary>
        /// <param name="syncEntityStatus">SyncEntityStatus to record sync entity information with agent information.</param>
        /// <returns>True, it indicate persistence successfull.</returns>
        public bool SetSyncEntityStatus(ISyncEntityStatus syncEntityStatus)
        {
            try
            {
                _repository.CreateSyncEntityStatus(syncEntityStatus);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        #endregion
    }
}