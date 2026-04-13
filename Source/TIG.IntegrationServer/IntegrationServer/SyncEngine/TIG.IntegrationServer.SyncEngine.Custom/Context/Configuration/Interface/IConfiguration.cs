using System;
using System.Collections.Generic;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Interface
{
    public interface IConfiguration : IDisposable
    {
        #region Settings

        int ConcurrentSyncTaskLimit { get; }
        int ConcurrentSyncInstanceBundleTasksPerSyncEntityBundleTaskLimit { get; }
        int ConcurrentSyncInstanceTasksPerSyncInstanceBundleTaskLimit { get; }
        TimeSpan ActiveSyncTasksPollingTimeout { get; }
        TimeSpan DefaultSyncTaskTimeout { get; }

        #endregion


        #region Safe Operations

        /// <summary>
        /// Get all active sync entity bundles
        /// </summary>
        /// <returns>Ids of active sync entity bundles</returns>
        IEnumerable<Guid> GetIdsOfActiveSyncEntityBundles();

        /// <summary>
        /// Check active sync task exists or not
        /// </summary>
        /// <param name="syncEntityBundleId">Sync entity bundle id</param>
        /// <returns>True, indicate active sync task exists</returns>
        bool IsActiveSyncTaskExists(Guid syncEntityBundleId);

        /// <summary>
        /// Get sync task timeout.
        /// </summary>
        /// <param name="syncTaskId">Sync task id</param>
        /// <returns>Time span to check task is timeout or not.</returns>
        TimeSpan GetSyncTaskTimeout(Guid syncTaskId);

        /// <summary>
        /// Get task data by entity bundle id
        /// </summary>
        /// <param name="entityBundleId">Sync entity bundle id</param>
        /// <returns>Task context</returns>
        ITaskData GetTaskData(Guid entityBundleId);

        #endregion

    }
}
