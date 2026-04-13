using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using Newtonsoft.Json;
using TIG.IntegrationServer.Common;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Constant;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.Helper;
using TIG.IntegrationServer.Plugin.Core.MapperPlugin.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task
{
    public class SyncInstanceTask : TaskBase, ISyncInstanceTask
    {
        #region Private Fields

        private readonly SyncInstance _sourceSyncInstance;
        private readonly SyncEntityMap _entityMap;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with components
        /// </summary>
        /// <param name="log">Log writer</param>
        /// <param name="context">Task context</param>
        /// <param name="taskData">Task data for hand persistence logic</param>
        /// <param name="sourceSyncInstance">Source sync instance</param>
        /// <param name="entityMap">Sync entity map</param>
        public SyncInstanceTask(
            ILog log,
            IContext context,
            ITaskData taskData,
            SyncInstance sourceSyncInstance,
            SyncEntityMap entityMap)
            : base(log, context, taskData)
        {
            _sourceSyncInstance = sourceSyncInstance;
            _entityMap = entityMap;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Execute sync task
        /// </summary>
        protected override void ExecuteTask()
        {
            using (var sourceAgent = GetAgent(_entityMap.SourceEntity))
            {
                using (var targetAgent = GetAgent(_entityMap.TargetEntity))
                {
                    var syncKeyInfo = _entityMap.GetSyncKeyInfo();
                    var sourceODataKey = _entityMap.SourceEntity.GetODataKey();
                    var sourceODataKeyType = Type.GetType(sourceODataKey.Type);

                    var fieldInfo = new List<EntityFieldInfo>();
                    var joinEntityInfo = new List<EntityJoinInfo>();

                    FieldInfoHelper.GetFieldInfosWithJoinInfo(_entityMap, fieldInfo, joinEntityInfo);

                    CriteriaOperator filter = new BinaryOperator(new QueryOperand(sourceODataKey.Name, EntityAlias.DefaultEntityAlias, DBColumn.GetColumnType(sourceODataKeyType)),
                                new ParameterValue { Value = _sourceSyncInstance.EntityRawId }, BinaryOperatorType.Equal);

                    if (_entityMap.SourceFilter != null)
                    {
                        // We cannot use CriteriaOperator.Parse method to get criteria operator from string.
                        // Because the Parse method does not work with a string returned by the CriteriaOperator.ToString method, if criteria contain object references.
                        // https://documentation.devexpress.com/#CoreLibraries/CustomDocument5411
                        var extraFilter = JsonConvert.DeserializeObject<CriteriaOperator>(_entityMap.SourceFilter);

                        filter = CriteriaOperator.And(extraFilter, filter);
                    }

                    // Get source entities
                    var sourceEntities = sourceAgent.ReadAll(_entityMap.SourceEntity.EntityName, fieldInfo, filter, joinEntityInfo.ToArray());

                    if (sourceEntities != null)
                    {
                        foreach (var sourceEntity in sourceEntities)
                        {
                            // Sync entity
                            SynchronizeEntity(sourceEntity, syncKeyInfo, sourceAgent, targetAgent);
                        }
                    }
                }
            }

            TaskData.MarkAsSynced(_sourceSyncInstance);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Synchronize entity
        /// </summary>
        /// <param name="sourceEntity">Source sync entity</param>
        /// <param name="syncKeyInfo">Sync key information</param>
        /// <param name="sourceAgent">Source agent to handle persistence logic</param>
        /// <param name="targetAgent">Target agent to handle persistence logic</param>
        private void SynchronizeEntity(IEntity sourceEntity, SyncKeyInfo syncKeyInfo, IAgent sourceAgent, IAgent targetAgent)
        {
            if (sourceEntity == null)
            {
                Log.Warn(string.Format("Source entity: {0}({1}) not be found.",
                            _entityMap.SourceEntity.EntityName,
                            _sourceSyncInstance.EntityRawId));
                return;
            }

            var targetSyncType = Type.GetType(syncKeyInfo.Target.Type);
            var targetSyncKeyValue = sourceEntity.GetValue(syncKeyInfo.Source.Name);

            if (syncKeyInfo.Target.Captial)
            {
                targetSyncKeyValue = targetSyncKeyValue.ToString().ToUpper();
            }

            // Get target entity by target agent
            CriteriaOperator filter = new BinaryOperator(new QueryOperand(syncKeyInfo.Target.Name, EntityAlias.DefaultEntityAlias, DBColumn.GetColumnType(targetSyncType)), new ParameterValue { Value = targetSyncKeyValue }, BinaryOperatorType.Equal);
            if (syncKeyInfo.TargetIdentities != null && syncKeyInfo.TargetIdentities.Any())
            {
                var subFilters = syncKeyInfo.TargetIdentities.Select(targetIdentity => new BinaryOperator(new QueryOperand(targetIdentity.Name, EntityAlias.DefaultEntityAlias, DBColumn.GetColumnType(Type.GetType(targetIdentity.Type))), new ParameterValue { Value = targetIdentity.Value }, BinaryOperatorType.Equal)).ToList();
                filter = CriteriaOperator.And(filter, CriteriaOperator.And(subFilters));
            }

            var isNew = false;
            IEntity targetEntity = null;

            if (targetSyncKeyValue != null)
            {
                targetEntity = targetAgent.ReadOne(_entityMap.TargetEntity.EntityName,
                    new List<EntityFieldInfo> { new EntityFieldInfo(syncKeyInfo.Target.Name, targetSyncType) },
                    filter);

                isNew = targetEntity == null;
            }

            if (isNew)
            {
                targetEntity = targetAgent.BuildEmptyEntity();
            }

            // Apply mapping
            ApplyMapping(_entityMap, sourceEntity, targetEntity, sourceAgent, targetAgent, isNew);

            if (targetEntity != null
                && targetEntity.IsEmpty())
            {
                return;
            }

            var targetODataKey = _entityMap.TargetEntity.GetODataKey();
            object targetPrimaryKeyValue = null;

            // Build targe field information by target entity
            var targetFieldInfos = FieldInfoHelper.GetFieldInfos(targetEntity, _entityMap);

            if (targetFieldInfos == null
                || targetFieldInfos.Count == 0)
            {
                Log.Warn("System cannot get target entity information.");
                return;
            }

            if (isNew)
            {
                targetEntity = targetAgent.Create(_entityMap.TargetEntity.EntityName, targetFieldInfos, targetEntity);

                // Target sync key is not same as target primary key.
                // System need to update source sync key from target retrived entity.
                if (targetODataKey.Name != syncKeyInfo.Target.Name)
                {
                    // Update retrieved sync key from target entity to source entity.
                    UpdateRetrievedSyncKeyToSource(sourceEntity, sourceAgent, targetEntity, syncKeyInfo);
                }

                targetPrimaryKeyValue = targetEntity.GetValue(targetODataKey.Name);

                Guid key;
                if (Guid.TryParse(targetPrimaryKeyValue.ToString(), out key))
                {
                    targetPrimaryKeyValue = key.ToString("D");
                }
            }
            else
            {
                if (targetEntity != null)
                {
                    targetPrimaryKeyValue = targetEntity.GetValue(targetODataKey.Name);

                    if (targetPrimaryKeyValue != null)
                    {
                        var updateFilter = new BinaryOperator(new QueryOperand(targetODataKey.Name, null, DBColumn.GetColumnType(targetSyncType)), new ParameterValue() { Value = targetSyncKeyValue }, BinaryOperatorType.Equal);
                        targetAgent.Update(_entityMap.TargetEntity.EntityName, targetFieldInfos, targetEntity, updateFilter);
                    }
                }
            }

            // Record sync status to stop back flow
            if (targetEntity != null
                && targetPrimaryKeyValue != null)
            {
                var syncEntityStatus = new SyncEntityStatus
                {
                    EntityKey = targetPrimaryKeyValue.ToString(),
                    SourceAgentId = _entityMap.SourceEntity.AgentPluginId.ToString(),
                    TargetAgentId = _entityMap.TargetEntity.AgentPluginId.ToString(),
                    SynceTime = DateTime.Now
                };

                Context.SyncStatusManager.SetSyncEntityStatus(syncEntityStatus);

                var logInfo = string.Format(@"Entity:{0}({1}) has been synced from entity {2}.",
                    _entityMap.TargetEntity.EntityName,
                    targetPrimaryKeyValue,
                    _entityMap.SourceEntity.EntityName);

                Log.Info(logInfo);
            }
        }

        /// <summary>
        /// Update retrieved syncKey to source
        /// </summary>
        /// <param name="sourceEntity">Source sync entity</param>
        /// <param name="sourceAgent">Source agent for handle persistence logic</param>
        /// <param name="targetEntity">Target sync entity</param>
        /// <param name="syncKeyInfo">Sync key information for sync entity</param>
        private void UpdateRetrievedSyncKeyToSource(IEntity sourceEntity, IAgent sourceAgent,
            IEntity targetEntity, SyncKeyInfo syncKeyInfo)
        {
            var targetSyncKeyVal = targetEntity.GetValue(syncKeyInfo.Target.Name);
            sourceEntity.SetValue(syncKeyInfo.Source.Name, targetSyncKeyVal);
            var sourceODataKey = _entityMap.SourceEntity.GetODataKey();
            var sourceODataKeyType = Type.GetType(sourceODataKey.Type);

            var updateFilter =
                new BinaryOperator(new QueryOperand(sourceODataKey.Name, null, DBColumn.GetColumnType(sourceODataKeyType)),
                    new ParameterValue { Value = _sourceSyncInstance.EntityRawId }, BinaryOperatorType.Equal);
            var fieldInfo = new List<EntityFieldInfo>
            {
                new EntityFieldInfo (syncKeyInfo.Source.Name, Type.GetType(syncKeyInfo.Source.Type))
            };
            sourceAgent.Update(_entityMap.SourceEntity.EntityName, fieldInfo, sourceEntity, updateFilter);

            var souEntityStatus = new SyncEntityStatus
            {
                EntityKey = _sourceSyncInstance.EntityRawId,
                SourceAgentId = _entityMap.TargetEntity.AgentPluginId.ToString(),
                TargetAgentId = _entityMap.SourceEntity.AgentPluginId.ToString(),
                SynceTime = DateTime.Now
            };

            Context.SyncStatusManager.SetSyncEntityStatus(souEntityStatus);
        }

        /// <summary>
        /// GetAgent by sync entity
        /// </summary>
        /// <param name="syncEntity">Sync entity for get agent plugin.</param>
        /// <returns>Sync agent</returns>
        private IAgent GetAgent(SyncEntity syncEntity)
        {
            if (syncEntity.AgentPluginId == null)
            {
                Log.Error("Sync agent plugin cannot be null, please check configuration file and make sure it is right.");
                return null;
            }

            var agentSourcePluginId = syncEntity.AgentPluginId.Value;
            var agentSourcePlugin = AutofacLocator.ResolveAgentPlugin<IAgentPlugin>(agentSourcePluginId);
            var agent = agentSourcePlugin.BuildAgent();
            return agent;
        }

        /// <summary>
        /// Apply mapping for source entity
        /// </summary>
        /// <param name="entityMap">Sync entity map</param>
        /// <param name="sourceEntity">Source sync entity</param>
        /// <param name="targetEntity">Target sync entity</param>
        /// <param name="sourceAgent">Source agent for handle presistence logic</param>
        /// <param name="targetAgent">Target agent for handle presistence logic</param>
        /// <param name="isNew">True, target entity is new</param>
        private void ApplyMapping(SyncEntityMap entityMap, IEntity sourceEntity, IEntity targetEntity,
            IAgent sourceAgent, IAgent targetAgent, bool isNew)
        {
            try
            {
                var mapper = GetSyncMapper(entityMap);
                mapper.Map(entityMap,
                    sourceEntity,
                    targetEntity,
                    sourceAgent,
                    targetAgent,
                    isNew);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Failed to apply entity map with ID {0}", entityMap.Oid), ex);
            }
        }

        /// <summary>
        /// GetSyncMapper by entityMap
        /// </summary>
        /// <param name="entityMap">Sync entity map</param>
        /// <returns>Field mapper plugin</returns>
        private IFieldMapperPlugin GetSyncMapper(SyncEntityMap entityMap)
        {
            var mapperPluginId = entityMap.MapperPluginId;
            var mapperPlugin = AutofacLocator.ResolveMapperPlugin<IFieldMapperPlugin>(mapperPluginId);

            if (mapperPlugin == null)
            {
                throw new InvalidOperationException("Cannot find mapper by Id=" + mapperPluginId);
            }

            return mapperPlugin;
        }

        #endregion
    }
}
