using System;

namespace TIG.IntegrationServer.SyncEngine.Core.Interface
{
    public interface ISyncStatusManager
    {
        /// <summary>
        /// Get last sync entity time
        /// </summary>
        /// <param name="entityKey">Sync entity key</param>
        /// <param name="sourceAgentId">Source agent id</param>
        /// <param name="targetAgentId">Target agent id</param>
        /// <returns>Last sync entity time</returns>
        DateTime? GetLastSyncTime(object entityKey, Guid? sourceAgentId, Guid? targetAgentId);

        /// <summary>
        /// Set sync entity status
        /// </summary>
        /// <param name="syncEntityStatus">SyncEntityStatus to record sync entity information with agent information.</param>
        /// <returns>True, it indicate persistence successfull.</returns>
        bool SetSyncEntityStatus(ISyncEntityStatus syncEntityStatus);
    }
}