using System;

namespace TIG.IntegrationServer.SyncEngine.Core.Interface
{
    public interface ISyncStatusRepository
    {
        /// <summary>
        /// Get last sync time from persistence
        /// </summary>
        /// <param name="entityKey">Sync entity key</param>
        /// <param name="sourceAgentId">Source agent id</param>
        /// <param name="targetAgentId">Target agent id</param>
        /// <returns>Last sync entity time</returns>
        DateTime? GetLastSyncTime(string entityKey, string sourceAgentId, string targetAgentId);

        /// <summary>
        /// Create sync entity status in persistence
        /// </summary>
        /// <param name="syncEntityStatus">SyncEntityStatus to record sync entity information with agent information.</param>
        /// <returns>True, it indicate persistence successfull.</returns>
        void CreateSyncEntityStatus(ISyncEntityStatus syncEntityStatus);
    }
}