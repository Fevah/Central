using System;
using System.Collections.Generic;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data
{
    public interface ITaskData : IDisposable
    {
        /// <summary>
        /// Entity bundle to get current sync entity information
        /// </summary>
        SyncEntityBundle EntityBundle { get; }

        /// <summary>
        /// Entities for sync
        /// </summary>
        IEnumerable<SyncEntity> Entities { get; }

        /// <summary>
        /// ActiveEntities for get current active entity.
        /// </summary>
        IEnumerable<SyncEntity> ActiveEntities { get; }

        /// <summary>
        /// Get unsynced instance bundles
        /// </summary>
        /// <returns></returns>
        IEnumerable<SyncInstanceBundle> GetUnsyncedInstanceBundles();

        /// <summary>
        /// Create missing unsynced instances by current sync instance bundle
        /// </summary>
        /// <param name="syncInstanceBundle">sync instance bundle</param>
        void CreateMissingUnsyncedInstances(SyncInstanceBundle syncInstanceBundle);

        /// <summary>
        /// Get sync entity map by source entity
        /// </summary>
        /// <param name="syncEntity">Source sync entity</param>
        /// <returns>Retrieved sync entity maps</returns>
        List<SyncEntityMap> GetSyncEntityMap(SyncEntity syncEntity);

        /// <summary>
        /// Mark sync instance to synced after sync logic was done.
        /// </summary>
        /// <param name="syncInstance">SyncInstance which has been synced</param>
        void MarkAsSynced(SyncInstance syncInstance);

        /// <summary>
        /// Mark sync instance bundle as synced
        /// </summary>
        /// <param name="syncInstanceBundle">sync instance bundle which has been synced</param>
        void MarkAsSynced(SyncInstanceBundle syncInstanceBundle);

        /// <summary>
        /// Get sync instance information
        /// </summary>
        /// <param name="syncEntity">SyncEntity for sync source entity</param>
        /// <param name="entityRowId">Current sync entity row id</param>
        /// <returns>Retrieved sync instance</returns>
        SyncInstance GetSyncInstance(SyncEntity syncEntity, string entityRowId);

        /// <summary>
        /// Create sync instance
        /// </summary>
        /// <param name="entity">Sync source entity</param>
        /// <param name="entityRowId">Current sync entity row id</param>
        /// <param name="hash">Current sync entity hash</param>
        /// <returns>Retrieved sync instance</returns>
        SyncInstance CreateSyncInstance(SyncEntity entity, string entityRowId, string hash);

        /// <summary>
        /// Mark sync instance status to changed
        /// </summary>
        /// <param name="syncInstance">Sync instance which was changed</param>
        void MarkAsChanged(SyncInstance syncInstance);

        /// <summary>
        /// Update sync instance hash
        /// </summary>
        /// <param name="instance">Current sync instance</param>
        /// <param name="hash">Hash has been changed</param>
        void UpdateSyncInstanceHash(SyncInstance instance, string hash);

        /// <summary>
        /// Calculate high priority sync instance
        /// </summary>
        /// <param name="syncInstanceBundle">Sync instance bundle to sync</param>
        void LeaveOneClashWinner(SyncInstanceBundle syncInstanceBundle);

        /// <summary>
        /// Get agent plugin by sync entity.
        /// </summary>
        /// <param name="activeEntity">Active sync entity.</param>
        /// <returns>Sync agent plugin Id</returns>
        Guid GetAgentPluginId(SyncEntity activeEntity);

        /// <summary>
        /// Update track version Id
        /// </summary>
        /// <param name="syncEntity">Current sync entity</param>
        /// <param name="versionId">Synced version id to cached to database.</param>
        void UpdateTrackerVersionId(SyncEntity syncEntity, long versionId);
    }
}
