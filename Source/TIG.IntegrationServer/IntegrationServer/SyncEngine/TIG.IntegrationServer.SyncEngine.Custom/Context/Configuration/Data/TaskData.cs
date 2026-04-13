using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Integration;
using TIG.TotalLink.Shared.DataModel.Integration;
using TIG.TotalLink.Shared.Facade.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data
{
    public class TaskData : SyncEngineComponent, ITaskData
    {
        #region Private Fields

        private readonly Guid _entityBundleId;
        private readonly IIntegrationFacade _facade;

        private SyncEntityBundle _entityBundleCache;
        private IList<SyncEntity> _entitiesCache;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with required component
        /// </summary>
        /// <param name="log">Loger for write information</param>
        /// <param name="entityBundleId">Entity bundle id to get entity bundle entity</param>
        /// <param name="facade">Integration facade to handle persistence logic</param>
        public TaskData(ILog log, Guid entityBundleId, IIntegrationFacade facade)
            : base(log)
        {
            _entityBundleId = entityBundleId;
            _facade = facade;
        }

        #endregion


        #region ITaskData Members

        /// <summary>
        /// Entity bundle to get current sync entity information
        /// </summary>
        public SyncEntityBundle EntityBundle
        {
            get
            {
                RunDisposedCheck();

                if (_entityBundleCache == null)
                {
                    _facade.ExecuteUnitOfWork(uow =>
                    {
                        _entityBundleCache = uow.Query<SyncEntityBundle>().FirstOrDefault(i => i.Oid == _entityBundleId);
                        // Load entity to local
                        if (_entityBundleCache != null
                            && !_entityBundleCache.Entities.IsLoaded)
                        {
                            _entityBundleCache.Entities.Load();
                        }
                    });
                }

                return _entityBundleCache;
            }
        }

        /// <summary>
        /// Entities for sync
        /// </summary>
        public IEnumerable<SyncEntity> Entities
        {
            get
            {
                RunDisposedCheck();

                return _entitiesCache ?? (_entitiesCache = EntityBundle.Entities.ToList());
            }
        }

        /// <summary>
        /// ActiveEntities for get current active entity.
        /// </summary>
        public IEnumerable<SyncEntity> ActiveEntities
        {
            get
            {
                RunDisposedCheck();

                var activeEntities = Entities
                    .Where(i =>
                        !string.IsNullOrWhiteSpace(i.EntityName) &&
                        i.IsActive == true &&
                        i.AgentPluginId != default(Guid))
                    .ToArray();
                return activeEntities;
            }
        }

        /// <summary>
        /// Get unsynced instance bundles
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SyncInstanceBundle> GetUnsyncedInstanceBundles()
        {
            var result = _facade.ExecuteQuery(
                uow =>
                {
                    var instanceBundles =
                        uow.Query<SyncInstanceBundle>().Where(i => i.State == SyncInstanceBundleState.Unsynced &&
                                                                   i.EntityBundle.Oid == EntityBundle.Oid);
                    foreach (var syncInstanceBundle in instanceBundles)
                    {
                        if (!syncInstanceBundle.Instances.IsLoaded)
                        {
                            syncInstanceBundle.Instances.Load();
                        }
                    }
                    return instanceBundles;
                });
            return result;
        }

        /// <summary>
        /// Create missing unsynced instances by current sync instance bundle
        /// </summary>
        /// <param name="syncInstanceBundle">sync instance bundle</param>
        public void CreateMissingUnsyncedInstances(SyncInstanceBundle syncInstanceBundle)
        {
            RunDisposedCheck();

            _facade.ExecuteUnitOfWork(uow =>
            {
                var entityBundle = uow.GetObjectByKey<SyncEntityBundle>(syncInstanceBundle.EntityBundle.Oid);
                syncInstanceBundle = uow.GetObjectByKey<SyncInstanceBundle>(syncInstanceBundle.Oid);

                foreach (var entity in entityBundle.Entities.Where(entity => syncInstanceBundle.Instances.All(i => i.Entity.Oid != entity.Oid)))
                {
                    new SyncInstance(uow)
                    {
                        Oid = Guid.NewGuid(),
                        EntityRawId = null,
                        State = SyncInstanceState.UpdateRequired,
                        Entity = entity,
                        Bundle = syncInstanceBundle
                    };
                }
            });
        }

        /// <summary>
        /// Get sync entity map by source entity
        /// </summary>
        /// <param name="syncEntity">Source sync entity</param>
        /// <returns>Retrieved sync entity maps</returns>
        public List<SyncEntityMap> GetSyncEntityMap(SyncEntity syncEntity)
        {
            var relativeMap = _facade.ExecuteQuery(uow => uow.Query<SyncEntityMap>()
                .Where(item => item.SourceEntity.Oid == syncEntity.Oid && item.IsActive))
                .OrderBy(item => item.Order).ToList();

            return relativeMap;
        }

        /// <summary>
        /// Mark sync instance to synced after sync logic was done.
        /// </summary>
        /// <param name="syncInstance">SyncInstance which has been synced</param>
        public void MarkAsSynced(SyncInstance syncInstance)
        {
            _facade.ExecuteUnitOfWork(uow =>
             {
                 syncInstance = uow.GetObjectByKey<SyncInstance>(syncInstance.Oid);

                 if (syncInstance == null)
                 {
                     return;
                 }

                 if (syncInstance.Bundle.Instances.All(i => i.State == SyncInstanceState.Synced))
                 {
                     syncInstance.Bundle.State = SyncInstanceBundleState.Synced;
                 }

                 syncInstance.State = SyncInstanceState.Synced;
             });


            if (syncInstance.State != SyncInstanceState.Synced)
            {
                Log.Warn("Update sync instance status not succeed.");
            }
        }

        /// <summary>
        /// Mark sync instance bundle as synced
        /// </summary>
        /// <param name="syncInstanceBundle">sync instance bundle which has been synced</param>
        public void MarkAsSynced(SyncInstanceBundle syncInstanceBundle)
        {
            _facade.ExecuteUnitOfWork(uow =>
            {
                syncInstanceBundle = uow.GetObjectByKey<SyncInstanceBundle>(syncInstanceBundle.Oid);
                syncInstanceBundle.State = SyncInstanceBundleState.Synced;
            });
        }

        /// <summary>
        /// Get sync instance information
        /// </summary>
        /// <param name="syncEntity">SyncEntity for sync source entity</param>
        /// <param name="entityRowId">Current sync entity row id</param>
        /// <returns>Retrieved sync instance</returns>
        public SyncInstance GetSyncInstance(SyncEntity syncEntity, string entityRowId)
        {
            return _facade.ExecuteQuery(uow => uow.Query<SyncInstance>().Where(i => i.Entity.Oid == syncEntity.Oid && i.EntityRawId == entityRowId)).FirstOrDefault();
        }

        /// <summary>
        /// Create sync instance
        /// </summary>
        /// <param name="entity">Sync source entity</param>
        /// <param name="entityRowId">Current sync entity row id</param>
        /// <param name="hash">Current sync entity hash</param>
        /// <returns>Retrieved sync instance</returns>
        public SyncInstance CreateSyncInstance(SyncEntity entity, string entityRowId, string hash)
        {
            SyncInstance instance = null;
            _facade.ExecuteUnitOfWork(uow =>
            {
                var entityBundle = uow.GetObjectByKey<SyncEntityBundle>(EntityBundle.Oid);
                var instanceBundle = new SyncInstanceBundle(uow)
                {
                    EntityBundle = entityBundle,
                    State = SyncInstanceBundleState.Unsynced
                };

                var syncEntity = uow.GetObjectByKey<SyncEntity>(entity.Oid);

                instance = new SyncInstance(uow)
                {
                    EntityRawId = entityRowId,
                    Entity = syncEntity,
                    Bundle = instanceBundle,
                    State = SyncInstanceState.ChangeUnprocessed,
                    Hash = hash,
                    LastChangeCapturedTime = DateTime.UtcNow
                };
            });

            return instance;
        }

        /// <summary>
        /// Mark sync instance status to changed
        /// </summary>
        /// <param name="syncInstance">Sync instance which was changed</param>
        public void MarkAsChanged(SyncInstance syncInstance)
        {
            _facade.ExecuteUnitOfWork(uow =>
            {
                // Get instance from cache.
                syncInstance = uow.GetObjectByKey<SyncInstance>(syncInstance.Oid);

                // Update change time and set state to change and unprocessed. 
                syncInstance.LastChangeCapturedTime = DateTime.UtcNow;
                syncInstance.State = SyncInstanceState.ChangeUnprocessed;

                // Update owner (Sync instance bundle) state to un synced.
                syncInstance.Bundle.State = SyncInstanceBundleState.Unsynced;
            });
        }

        /// <summary>
        /// Update sync instance hash
        /// </summary>
        /// <param name="instance">Current sync instance</param>
        /// <param name="hash">Hash has been changed</param>
        public void UpdateSyncInstanceHash(SyncInstance instance, string hash)
        {
            _facade.ExecuteUnitOfWork(uow =>
            {
                instance = uow.GetObjectByKey<SyncInstance>(instance.Oid);
                instance.Hash = hash;
            });
        }

        /// <summary>
        /// Calculate high priority sync instance
        /// </summary>
        /// <param name="syncInstanceBundle">Sync instance bundle to sync</param>
        public void LeaveOneClashWinner(SyncInstanceBundle syncInstanceBundle)
        {
            _facade.ExecuteUnitOfWork(uow =>
                {
                    syncInstanceBundle = uow.GetObjectByKey<SyncInstanceBundle>(syncInstanceBundle.Oid);
                    var changedInstances = syncInstanceBundle.Instances
                        .Where(i => i.State == SyncInstanceState.ChangeUnprocessed).ToList();

                    if (changedInstances.Any())
                    {
                        if (changedInstances.Select(i => i.Entity.PriorityInBundle).All(i => i == null))
                        {
                            var message = string.Format("Entity bundle with {0} has no prioritised entity.", syncInstanceBundle.EntityBundle.Oid);
                            var ex = new InvalidOperationException(message);
                            Log.Error(message, ex);
                            throw ex;
                        }

                        var highPriorityInstance = changedInstances.FirstOrDefault(i => i.Entity != null && i.Entity.PriorityInBundle.HasValue);

                        if (highPriorityInstance == null)
                        {
                            var message = string.Format("Entity bundle with {0} has no prioritised entity.", syncInstanceBundle.EntityBundle.Oid);
                            var ex = new InvalidOperationException(message);
                            Log.Error(message, ex);
                            throw ex;
                        }

                        foreach (var instance in changedInstances)
                        {
                            if (highPriorityInstance.Entity.PriorityInBundle != null
                                && instance.Entity.PriorityInBundle.HasValue
                                && instance.Entity.PriorityInBundle.Value >= highPriorityInstance.Entity.PriorityInBundle.Value)
                            {
                                highPriorityInstance = instance;
                            }
                        }

                        // Mark loser to unresolved.
                        foreach (var changeinstance in changedInstances.Where(i => i.Oid != highPriorityInstance.Oid))
                        {
                            changeinstance.State = SyncInstanceState.Unresolved;
                        }
                    }
                });
        }

        /// <summary>
        /// Get agent plugin by sync entity.
        /// </summary>
        /// <param name="activeEntity">Active sync entity.</param>
        /// <returns>Sync agent plugin Id</returns>
        public Guid GetAgentPluginId(SyncEntity activeEntity)
        {
            return activeEntity.AgentPluginId ?? Guid.Empty;
        }

        /// <summary>
        /// Update track version Id
        /// </summary>
        /// <param name="syncEntity">Current sync entity</param>
        /// <param name="versionId">Synced version id to cached to database.</param>
        public void UpdateTrackerVersionId(SyncEntity syncEntity, long versionId)
        {
            _facade.ExecuteUnitOfWork(uow =>
            {
                syncEntity = uow.GetObjectByKey<SyncEntity>(syncEntity.Oid);
                syncEntity.LastChangeTrackerVersionId = versionId;
            });
        }

        #endregion
    }
}
