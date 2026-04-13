using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using TIG.IntegrationServer.Common;
using TIG.IntegrationServer.Common.Extension;
using TIG.IntegrationServer.Common.MappingEntity;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Constant;
using TIG.IntegrationServer.Plugin.Core.ConverterPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.Expression;
using TIG.IntegrationServer.Plugin.Core.Helper;
using TIG.IntegrationServer.Plugin.Core.MapperPlugin.Interface;
using TIG.IntegrationServer.SyncEngine.Core.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;
using Action = TIG.IntegrationServer.Common.MappingEntity.Action;
using Field = TIG.IntegrationServer.Common.MappingEntity.Field;

namespace TIG.IntegrationServer.Plugin.Mapper.DictionaryEntityMapper
{
    public class DictionaryEntityMapper : IFieldMapperPlugin
    {
        #region Private Properties

        private readonly ILog _log;
        private readonly ISyncStatusManager _syncStatusManager;
        private const string EnumIdentity = "Enum.";

        #endregion


        #region Consturctors

        /// <summary>
        /// Constructor for dictionary entity mapping
        /// </summary>
        /// <param name="log">Log for record system information.</param>
        /// <param name="syncStatusManager">System status manager for manage sync entity status.</param>
        public DictionaryEntityMapper(ILog log, ISyncStatusManager syncStatusManager)
        {
            _log = log;
            _syncStatusManager = syncStatusManager;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Map method for mapping source object to target object.
        /// </summary>
        /// <param name="syncEntityMap">Entity mapping for mapping entity.</param>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="targetEntity">Target entity for mapping</param>
        /// <param name="sourceAgent">Source agent to handle persistence operate.</param>
        /// <param name="targetAgent">Target agent to handle persistence operate.</param>
        /// <param name="isNew">Indicate current mapping object is new or not.</param>
        public void Map(SyncEntityMap syncEntityMap, IEntity sourceEntity, IEntity targetEntity,
            IAgent sourceAgent, IAgent targetAgent, bool isNew)
        {
            var mapping = syncEntityMap.GetSyncMapping();

            var source = sourceEntity as IDictionary<string, object>;
            if (source == null)
            {
                return;
            }

            switch (mapping.EntityMappings.Action)
            {
                case Action.Combine:
                    MapForCombination(syncEntityMap, source, targetEntity, sourceAgent, targetAgent, isNew);
                    break;
                case Action.Separate:
                    MapForSepartion(syncEntityMap, source, targetEntity, targetAgent, isNew);
                    break;
                case Action.Default:
                    MapForSingle(syncEntityMap, source, targetEntity, targetAgent, isNew);
                    break;
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// MapForSingle method for one to one mapping.
        /// </summary>
        /// <param name="syncEntityMap">Sync entity map for mapping entitys</param>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="targetEntity">Target entity for mapping</param>
        /// <param name="targetAgent">Target agent for handle persistence logic</param>
        /// <param name="isNew">True, indicate target entity is new.</param>
        private void MapForSingle(SyncEntityMap syncEntityMap, IDictionary<string, object> sourceEntity, IEntity targetEntity, IAgent targetAgent, bool isNew)
        {
            var mapping = syncEntityMap.GetSyncMapping();
            var syncEntityMapping = mapping.EntityMappings.EntityMapping.FirstOrDefault();

            if (syncEntityMapping == null)
            {
                return;
            }

            // Mapping fields
            foreach (var fieldMapping in syncEntityMapping.FieldMapping)
            {
                MappingField(sourceEntity, targetEntity, fieldMapping, mapping.Enums, targetAgent, isNew);
            }
        }

        /// <summary>
        /// MapForSepartion for mapping entities one to many.
        /// </summary>
        /// <param name="syncEntityMap">Sync entity map for mapping entitys</param>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="targetEntity">Target entity for mapping</param>
        /// <param name="targetAgent">Target agent for handle persistence logic</param>
        /// <param name="isNew">True, indicate target entity is new.</param>
        private void MapForSepartion(SyncEntityMap syncEntityMap,
            IDictionary<string, object> sourceEntity, IEntity targetEntity, IAgent targetAgent,
            bool isNew)
        {
            var mapping = syncEntityMap.GetSyncMapping();
            var entityName = syncEntityMap.SourceEntity.EntityName;

            // Update entity 
            // Only update entity self without parent.
            if (!isNew)
            {
                var entityMapping = GetSyncEntityMapping(mapping, entityName, sourceEntity);

                foreach (var fieldMapping in entityMapping.FieldMapping)
                {
                    MappingField(sourceEntity, targetEntity, fieldMapping, mapping.Enums, targetAgent, false);
                }

                return;
            }

            // Separate Entity to mutil entites 
            var relativeEntities = new Dictionary<string, IEntity>();

            // According to mapping to create a new entities.
            foreach (var entityMapping in mapping.EntityMappings.EntityMapping.Where(em => em.Name == entityName))
            {
                var entity = targetAgent.BuildEmptyEntity();
                var fieldsInfos = new List<EntityFieldInfo>();

                // Mapping field and get target field infos.
                foreach (var targetFieldInfos in entityMapping.FieldMapping.Select(fieldMapping => MappingField(sourceEntity, entity, fieldMapping, mapping.Enums, targetAgent, false)))
                {
                    fieldsInfos.AddRange(targetFieldInfos);
                }

                // Record sync status for stop back flow.
                var retrivedEntity = targetAgent.Create(entityName, fieldsInfos, entity);

                var syncKeyInfo = syncEntityMap.TargetEntity.GetODataKey();
                var targetKey = retrivedEntity.GetValue(syncKeyInfo.Name);

                var sourceEntityStatus = new SyncEntityStatus
                {
                    EntityKey = targetKey.ToString(),
                    SourceAgentId = syncEntityMap.SourceEntity.AgentPluginId.ToString(),
                    TargetAgentId = syncEntityMap.TargetEntity.AgentPluginId.ToString(),
                    SynceTime = DateTime.Now
                };

                _syncStatusManager.SetSyncEntityStatus(sourceEntityStatus);

                relativeEntities.Add(entityMapping.Id, retrivedEntity);
            }

            // Create relationship for entities.
            foreach (var mappings in mapping.EntityRelationship.Mappings)
            {
                var relationshipEntity = targetAgent.BuildEmptyEntity();

                var targetFields = new List<EntityFieldInfo>();

                foreach (var fieldMapping in mappings.Mapping)
                {
                    switch (fieldMapping.MappingType)
                    {
                        // Link two entities which create as above.
                        case MappingType.Link:
                            // Get link entity by entity key.
                            var linkEntity = relativeEntities[fieldMapping.Value];
                            relationshipEntity.SetValue(fieldMapping.FieldName, linkEntity);
                            targetFields.Add(new EntityFieldInfo(fieldMapping.FieldName));
                            break;
                        // Link entity from persistence. 
                        case MappingType.Entity:
                            var keyType = Type.GetType(fieldMapping.Type);
                            var filter = new BinaryOperator(new QueryOperand(fieldMapping.Key, EntityAlias.DefaultEntityAlias, DBColumn.GetColumnType(keyType)),
                                    new ParameterValue { Value = fieldMapping.Value }, BinaryOperatorType.Equal);

                            var fieldInfos = new List<EntityFieldInfo>
                            {
                                new EntityFieldInfo(fieldMapping.Key, keyType)
                            };

                            var entity = targetAgent.ReadOne(fieldMapping.Entity, fieldInfos, filter);
                            if (entity != null)
                            {
                                relationshipEntity.SetValue(fieldMapping.FieldName, entity);
                                targetFields.Add(new EntityFieldInfo(fieldMapping.FieldName));
                            }
                            break;
                    }
                }

                // Create link relatinship entity.
                var retrivedEntity = targetAgent.Create(mapping.EntityRelationship.Entity, targetFields, relationshipEntity);
                var targetKey = retrivedEntity.GetValue(mapping.EntityRelationship.Key);

                // Record it to sync entity status to stop back flow
                var sourceEntityStatus = new SyncEntityStatus
                {
                    EntityKey = targetKey.ToString(),
                    SourceAgentId = syncEntityMap.SourceEntity.AgentPluginId.ToString(),
                    TargetAgentId = syncEntityMap.TargetEntity.AgentPluginId.ToString(),
                    SynceTime = DateTime.Now
                };

                _syncStatusManager.SetSyncEntityStatus(sourceEntityStatus);
            }
        }

        #region Map for combination

        /// <summary>
        /// MapForCombination for mapping entities many to one.
        /// </summary>
        /// <param name="syncEntityMap">Sync entity map for mapping entitys</param>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="targetEntity">Target entity for mapping</param>
        /// <param name="sourceAgent">Source agent for handle persistence logic</param>
        /// <param name="targetAgent">Target agent for handle persistence logic</param>
        /// <param name="isNew">True, indicate target entity is new.</param>
        private void MapForCombination(SyncEntityMap syncEntityMap,
            IDictionary<string, object> sourceEntity, IEntity targetEntity,
            IAgent sourceAgent, IAgent targetAgent,
            bool isNew)
        {
            var mapping = syncEntityMap.GetSyncMapping();
            var entityName = syncEntityMap.SourceEntity.EntityName;

            var syncEntityMapping = GetSyncEntityMapping(mapping, entityName, sourceEntity);

            if (syncEntityMapping == null)
            {
                return;
            }

            // Mapping targent entity.
            if (targetEntity != null)
            {
                foreach (var fieldMapping in syncEntityMapping.FieldMapping)
                {
                    MappingField(sourceEntity, targetEntity, fieldMapping, mapping.Enums, targetAgent, isNew);
                }
            }

            if (syncEntityMapping.RelativeQuery == null)
            {
                return;
            }

            // Sync relative entities.
            // Build source fieldinfos
            var sourceFieldInfos = FieldInfoHelper.GetFieldInfos((IEntity)sourceEntity, syncEntityMap);

            // Mapping and update relative entity.
            if (syncEntityMapping.Action == Action.Update
                && !isNew)
            {
                MappingAndUpdateRelativeEntityForUpdate(sourceEntity, sourceAgent, sourceFieldInfos, targetAgent,
                    syncEntityMap, syncEntityMapping);
                return;
            }

            // Mapping entity for new.
            if (syncEntityMapping.Action == Action.All)
            {
                MappingEntityForNew(mapping, entityName, targetEntity, sourceAgent, targetAgent, isNew,
                    syncEntityMapping, sourceEntity, sourceFieldInfos);
            }
        }

        /// <summary>
        /// MappingAndUpdateRelativeEntityForUpdate for update relative entity
        /// </summary>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="sourceAgent">Source agent for handle persistence logic</param>
        /// <param name="sourceFieldInfos">Source entity field infos</param>
        /// <param name="targetAgent">Target agent for handle persistence logic</param>
        /// <param name="syncEntityMap">Sync entity map for mapping entitys</param>
        /// <param name="syncEntityMapping">Sync entity mapping information</param>
        private void MappingAndUpdateRelativeEntityForUpdate(
            IDictionary<string, object> sourceEntity,
            IAgent sourceAgent,
            List<EntityFieldInfo> sourceFieldInfos,
            IAgent targetAgent,
            SyncEntityMap syncEntityMap,
            EntityMapping syncEntityMapping)
        {
            var relativeEntities = new List<IDictionary<string, object>>();

            // Nested to get relative entities.
            GetTargetRelativeEntities(sourceAgent, sourceEntity, sourceFieldInfos, syncEntityMapping.RelativeQuery,
                relativeEntities);

            // Sync relative entities.
            foreach (var relativeEntity in relativeEntities)
            {
                SyncRelativeEntity(sourceEntity, relativeEntity, syncEntityMap, syncEntityMapping, sourceAgent, targetAgent);
            }
        }

        /// <summary>
        /// SyncRelativeEntity for sync relative entity.
        /// </summary>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="sourceAgent">Source agent for handle persistence logic</param>
        /// <param name="sourceRelativeEntity">Source relative entity</param>
        /// <param name="targetAgent">Target agent for handle persistence logic</param>
        /// <param name="syncEntityMap">Sync entity map for mapping entitys</param>
        /// <param name="syncEntityMapping">Sync entity mapping information</param>
        private void SyncRelativeEntity(IDictionary<string, object> sourceEntity,
            IDictionary<string, object> sourceRelativeEntity,
            SyncEntityMap syncEntityMap,
            EntityMapping syncEntityMapping,
            IAgent sourceAgent,
            IAgent targetAgent)
        {
            bool isNew;
            var syncKeyInfo = syncEntityMap.GetSyncKeyInfo();
            var targetEntity = GetTargetEntity(syncEntityMap, sourceRelativeEntity, targetAgent, out isNew);

            var mapping = syncEntityMap.GetSyncMapping();

            var targetFieldInfos = new List<EntityFieldInfo>();

            // Mapping field and build field infos.
            foreach (var targetFieldInfo in syncEntityMapping.FieldMapping.Select(fieldMapping => MappingField(sourceEntity, targetEntity, fieldMapping, mapping.Enums, targetAgent, isNew)))
            {
                targetFieldInfos.AddRange(targetFieldInfo);
            }

            var targetODataKey = syncEntityMap.TargetEntity.GetODataKey();
            var targetODataKeyType = Type.GetType(targetODataKey.Type);
            object primaryKeyVal;

            if (isNew)
            {
                // Create a target entity.
                targetEntity = targetAgent.Create(syncEntityMap.TargetEntity.EntityName, targetFieldInfos, targetEntity);

                // Target sync key is not same as target primary key.
                // System need to update source sync key from target retrived entity.
                if (targetODataKey.Name != syncKeyInfo.Target.Name)
                {
                    // Update retrieved sync key from target entity to source entity.
                    var targetSyncKeyVal = targetEntity.GetValue(syncKeyInfo.Target.Name);
                    sourceEntity[syncKeyInfo.Source.Name] = targetSyncKeyVal;
                    var sourceODataKey = syncEntityMap.SourceEntity.GetODataKey();
                    var sourceKey = sourceEntity[sourceODataKey.Name];
                    var sourceODataKeyType = Type.GetType(sourceODataKey.Type);

                    var updateFilter = new BinaryOperator(
                            new QueryOperand(sourceODataKey.Name, null, DBColumn.GetColumnType(sourceODataKeyType)),
                            new ParameterValue { Value = targetSyncKeyVal }, BinaryOperatorType.Equal);

                    var updateFieldInfo = new List<EntityFieldInfo>
                    {
                        new EntityFieldInfo {Name = syncKeyInfo.Source.Name, Type = Type.GetType(syncKeyInfo.Source.Type)}
                    };

                    sourceAgent.Update(syncEntityMap.SourceEntity.EntityName, updateFieldInfo,
                        EntityBase.Map(sourceEntity),
                        updateFilter);

                    // Record it to sync entity status to stop back flow.
                    var souEntityStatus = new SyncEntityStatus
                    {
                        EntityKey = sourceKey.ToString(),
                        SourceAgentId = syncEntityMap.TargetEntity.AgentPluginId.ToString(),
                        TargetAgentId = syncEntityMap.SourceEntity.AgentPluginId.ToString(),
                        SynceTime = DateTime.Now
                    };

                    _syncStatusManager.SetSyncEntityStatus(souEntityStatus);
                }

                primaryKeyVal = targetEntity.GetValue(targetODataKey.Name);

                Guid key;
                if (Guid.TryParse(primaryKeyVal.ToString(), out key))
                {
                    primaryKeyVal = key.ToString("D");
                }
            }
            else
            {
                // Update target entity
                primaryKeyVal = targetEntity.GetValue(targetODataKey.Name);
                if (primaryKeyVal != null)
                {
                    var updateFilter = new BinaryOperator(new QueryOperand(targetODataKey.Name, null, DBColumn.GetColumnType(targetODataKeyType)),
                            new ParameterValue { Value = primaryKeyVal }, BinaryOperatorType.Equal);
                    targetAgent.Update(syncEntityMap.TargetEntity.EntityName, targetFieldInfos, targetEntity, updateFilter);
                }
            }

            // Record sync entity status to stop back flow.
            var syncEntityStatus = new SyncEntityStatus
            {
                EntityKey = primaryKeyVal.ToString(),
                SourceAgentId = syncEntityMap.SourceEntity.AgentPluginId.ToString(),
                TargetAgentId = syncEntityMap.TargetEntity.AgentPluginId.ToString(),
                SynceTime = DateTime.Now
            };

            _syncStatusManager.SetSyncEntityStatus(syncEntityStatus);

            var logInfo = string.Format(@"Entity:{0}({1}) has been synced from entity {2}.",
                syncEntityMap.TargetEntity.EntityName,
                primaryKeyVal,
                syncEntityMap.SourceEntity.EntityName);

            _log.Info(logInfo);
        }

        /// <summary>
        /// Get target entity by sync entity mapping
        /// </summary>
        /// <param name="syncEntityMap">Sync entity map for sync specification.</param>
        /// <param name="sourceEntity">Source sync entity</param>
        /// <param name="targetAgent">Target agent for handle persistence logic.</param>
        /// <param name="isNew">True, indicate sync target entity is new.</param>
        /// <returns>Retieved target entity</returns>
        private IEntity GetTargetEntity(SyncEntityMap syncEntityMap, IDictionary<string, object> sourceEntity,
            IAgent targetAgent, out bool isNew)
        {
            var syncKeyInfo = syncEntityMap.GetSyncKeyInfo();
            var targetSyncType = Type.GetType(syncKeyInfo.Target.Type);
            var targetSyncKeyVal = sourceEntity[syncKeyInfo.Source.Name];

            if (syncKeyInfo.Target.Captial)
            {
                targetSyncKeyVal = targetSyncKeyVal.ToString().ToUpper();
            }

            // Build query for get target entity.
            CriteriaOperator filter = null;
            // If there are any sub queries.
            if (syncKeyInfo.TargetIdentities != null && syncKeyInfo.TargetIdentities.Any())
            {
                var subFilters = syncKeyInfo.TargetIdentities.Select(targetIdentity =>
                    new BinaryOperator(
                        new QueryOperand(targetIdentity.Name, EntityAlias.DefaultEntityAlias,
                            DBColumn.GetColumnType(Type.GetType(targetIdentity.Type))),
                        new ParameterValue { Value = targetIdentity.Value }, BinaryOperatorType.Equal)).ToList();
                filter = CriteriaOperator.And(subFilters);
            }

            filter = CriteriaOperator.And(filter,
                new BinaryOperator(new QueryOperand(syncKeyInfo.Target.Name, EntityAlias.DefaultEntityAlias,
                        DBColumn.GetColumnType(targetSyncType)), new ParameterValue { Value = targetSyncKeyVal },
                    BinaryOperatorType.Equal));

            isNew = true;
            IEntity targetEntity = null;

            if (targetSyncKeyVal != null)
            {
                targetEntity = targetAgent.ReadOne(syncEntityMap.TargetEntity.EntityName,
                    new List<EntityFieldInfo> { new EntityFieldInfo(syncKeyInfo.Target.Name, targetSyncType) },
                    filter);

                isNew = targetEntity == null;
            }

            // If target entity not exist, create a empty target entity.
            if (isNew)
            {
                targetEntity = targetAgent.BuildEmptyEntity();
            }
            return targetEntity;
        }

        /// <summary>
        /// Get target relative entities after target entity was changed.
        /// </summary>
        /// <param name="sourceAgent">Source agent for handle persistence logic.</param>
        /// <param name="sourceEntity"></param>
        /// <param name="sourceFieldInfos"></param>
        /// <param name="relativeQuery"></param>
        /// <param name="relativeEntities"></param>
        private void GetTargetRelativeEntities(IAgent sourceAgent,
            IDictionary<string, object> sourceEntity,
            List<EntityFieldInfo> sourceFieldInfos,
            RelativeQuery relativeQuery,
            ICollection<IDictionary<string, object>> relativeEntities)
        {
            object keyValue;
            if (!sourceEntity.TryGetValue(relativeQuery.Key, out keyValue))
            {
                return;
            }

            var queryDescriptor = relativeQuery.QueryDescriptor;

            //Default alias is N0
            var alias = EntityAlias.DefaultEntityAlias;
            var fieldName = queryDescriptor.Query;
            var expandInfos = new List<EntityJoinInfo>();

            // Split query sections.
            var sections = queryDescriptor.Query.Split('.');

            // Get entity in query, if query like "Target.Oid" 
            if (sections.Length == 2)
            {
                expandInfos.Add(new EntityJoinInfo { EntityName = sections[0], FieldInfos = sourceFieldInfos });

                alias = "N1";
                fieldName = sections[1];
            }

            var fieldInfos = new List<EntityFieldInfo>();

            // Build query entity information.
            BuildQueryEntityInfo(sourceFieldInfos, queryDescriptor, fieldInfos, expandInfos);

            var filter = new BinaryOperator(new QueryOperand(fieldName, alias, DBColumn.GetColumnType(Type.GetType(queryDescriptor.Type))),
                    new ParameterValue { Value = keyValue }, BinaryOperatorType.Equal);

            var entities = sourceAgent.ReadAll(queryDescriptor.Entity, fieldInfos, filter, expandInfos.ToArray());

            var filterKey = QueryHelper.GetQueryKey(queryDescriptor.Target.Filter);
            var filterValue = QueryHelper.GetQueryValue(queryDescriptor.Target.Filter);
            foreach (var entity in entities.OfType<IDictionary<string, object>>())
            {
                // Find current targe entity by target property.
                if (queryDescriptor.Target != null)
                {
                    var dataEntity =
                        ODataQueryHelper.GetProperty(queryDescriptor.Target.Property, entity) as
                            IDictionary<string, object>;

                    if (dataEntity == null)
                    {
                        continue;
                    }

                    var sourcePropertyValue = ODataQueryHelper.GetProperty(filterKey, dataEntity);
                    // If current entity match with filter key value,
                    // The means it relative with current target entity.
                    if (Equals(sourcePropertyValue, filterValue))
                    {
                        relativeEntities.Add(dataEntity);
                    }
                    else
                    {
                        GetTargetRelativeEntities(sourceAgent, dataEntity, sourceFieldInfos, relativeQuery,
                            relativeEntities);
                    }
                }
            }
        }

        private void MappingEntityForNew(SyncMapping mapping,
            string entityName,
            IEntity targetEntity,
            IAgent sourceAgent,
            IAgent targetAgent,
            bool isNew,
            EntityMapping syncEntityMapping,
            IDictionary<string, object> source,
            List<EntityFieldInfo> sourceFieldInfos)
        {
            object keyVal;
            if (!source.TryGetValue(syncEntityMapping.RelativeQuery.Key, out keyVal))
            {
                return;
            }

            var queryDescriptor = syncEntityMapping.RelativeQuery.QueryDescriptor;

            //Default alias is N0
            var alias = EntityAlias.DefaultEntityAlias;
            var fieldName = queryDescriptor.Query;
            var expandInfos = new List<EntityJoinInfo>();

            // Get extra information by sub query: Query: 'Target.Oid'
            var sections = queryDescriptor.Query.Split('.');

            if (sections.Length == 2)
            {
                expandInfos.Add(new EntityJoinInfo { EntityName = sections[0], FieldInfos = sourceFieldInfos });

                alias = "N1";
                fieldName = sections[1];
            }

            var fieldInfos = new List<EntityFieldInfo>();

            BuildQueryEntityInfo(sourceFieldInfos, queryDescriptor, fieldInfos, expandInfos);

            var filter = new BinaryOperator(new QueryOperand(fieldName, alias, DBColumn.GetColumnType(Type.GetType(queryDescriptor.Type))),
                    new ParameterValue { Value = keyVal }, BinaryOperatorType.Equal);

            do
            {
                var entity = sourceAgent.ReadOne(queryDescriptor.Entity, fieldInfos, filter, expandInfos.ToArray());

                // If don't find any relative object, system will do nothing
                var relativeEntity = entity as IDictionary<string, object>;
                if (relativeEntity == null)
                {
                    break;
                }

                // Find current targe entity by target property.
                if (queryDescriptor.Target != null)
                {
                    relativeEntity =
                        ODataQueryHelper.GetProperty(queryDescriptor.Target.Property, relativeEntity) as
                            IDictionary<string, object>;

                    if (relativeEntity == null)
                    {
                        break;
                    }
                }

                // Get sync entity mapping.
                var relativeMapping = GetSyncEntityMapping(mapping, entityName, relativeEntity);
                if (relativeMapping == null)
                {
                    break;
                }

                // Mapping fields.
                foreach (var fieldMapping in relativeMapping.FieldMapping)
                {
                    MappingField(relativeEntity, targetEntity, fieldMapping, mapping.Enums, targetAgent, isNew);
                }

                if (!relativeEntity.TryGetValue(syncEntityMapping.RelativeQuery.Key, out keyVal))
                {
                    break;
                }
            } while (syncEntityMapping.RelativeQuery.Recursion);
        }

        /// <summary>
        /// Build query entity info
        /// </summary>
        /// <param name="sourceFieldInfos"></param>
        /// <param name="queryDescriptor"></param>
        /// <param name="fieldInfos"></param>
        /// <param name="expandInfos"></param>
        private static void BuildQueryEntityInfo(List<EntityFieldInfo> sourceFieldInfos, QueryDescriptor queryDescriptor, ICollection<EntityFieldInfo> fieldInfos,
            ICollection<EntityJoinInfo> expandInfos)
        {
            foreach (var expand in queryDescriptor.Expands)
            {
                // If there are any sub entity in expand section,
                // System will automatic create a join entity for query.
                var expandSections = expand.Split('.');
                if (expandSections.Any())
                {
                    var parent = new EntityJoinInfo
                    {
                        EntityName = expandSections[0],
                        FieldInfos = sourceFieldInfos,
                        SubJoinEntityInfos = new List<EntityJoinInfo>()
                    };

                    fieldInfos.Add(new EntityFieldInfo(expandSections[0]));

                    foreach (var expandSection in expandSections.Skip(1))
                    {
                        parent.SubJoinEntityInfos.Add(new EntityJoinInfo
                        {
                            EntityName = expandSection,
                            FieldInfos = new List<EntityFieldInfo>()
                        });
                    }

                    expandInfos.Add(parent);
                }
                else
                {
                    var joinEntityInfo = new EntityJoinInfo
                    {
                        EntityName = expand,
                        FieldInfos = sourceFieldInfos
                    };

                    expandInfos.Add(joinEntityInfo);
                    fieldInfos.Add(new EntityFieldInfo(expand));
                }
            }
        }

        /// <summary>
        /// Get sync entity mapping by entity name
        /// </summary>
        /// <param name="mapping">Sync mapping defination</param>
        /// <param name="entityName">Sync entity name</param>
        /// <param name="sourceEntity">Source entity for sync</param>
        /// <returns>Sync entity mapping</returns>
        private static EntityMapping GetSyncEntityMapping(SyncMapping mapping, string entityName,
            IDictionary<string, object> sourceEntity)
        {
            return (from entityMapping in mapping.EntityMappings.EntityMapping.Where(em => em.Name == entityName)
                    let filterKey = QueryHelper.GetQueryKey(entityMapping.Filter)
                    where filterKey != null
                    let sourcePropertyValue = ODataQueryHelper.GetProperty(filterKey, sourceEntity)
                    let filterValue = QueryHelper.GetQueryValue(entityMapping.Filter)
                    where Equals(sourcePropertyValue, filterValue)
                    select entityMapping).FirstOrDefault();
        }

        #endregion

        /// <summary>
        /// Mapping field according to mapping file
        /// </summary>
        /// <param name="sourceEntity">Source entity for sync</param>
        /// <param name="targetEntity">Target entity for sync</param>
        /// <param name="mapping">Field mapping for sync specification</param>
        /// <param name="enums">Enums information for sync specification</param>
        /// <param name="targetAgent">Target agent for persistence operator</param>
        /// <param name="isNew">True, indicate sync target entity is new</param>
        /// <returns>Retrieved fieldInfo for persistence target entity</returns>
        private List<EntityFieldInfo> MappingField(IDictionary<string, object> sourceEntity,
            IEntity targetEntity,
            FieldMapping mapping,
            Enums enums,
            IAgent targetAgent,
            bool isNew)
        {
            var target = targetEntity as IDictionary<string, object>;

            if (target == null)
            {
                throw new ArgumentException("Target entity is not a valid dictionary entity.");
            }

            var targetFieldInfos = new List<EntityFieldInfo>();

            foreach (var targetField in mapping.TargetFields.Field)
            {
                var targetType = Type.GetType(targetField.Type);

                // Using convert to get target field value
                if (!string.IsNullOrEmpty(targetField.Convert))
                {
                    var targetValue = BuildTargetFieldValueByConvert(mapping, enums, targetAgent, targetField, sourceEntity);
                    // Truncation handle when converted value length great than target field length.
                    if (targetField.Length != 0 && targetValue.ToString().Length > targetField.Length)
                    {
                        targetValue = targetValue.ToString().Substring(0, targetField.Length);
                        _log.Warn(
                            string.Format("Sync {0} be truncation, system was removed longer than target length part.",
                                targetField.Name));
                    }

                    targetEntity.SetValue(targetField.Name, targetValue);
                    // Add target field info for persistence.
                    targetFieldInfos.Add(new EntityFieldInfo(targetField.Name, targetType));
                    continue;
                }

                // Mapping field without converter.
                var sourceField = mapping.SourceFields.Field.FirstOrDefault();
                if (sourceField == null)
                {
                    continue;
                }

                var sourceProperty = sourceEntity.FirstOrDefault(p => p.Key.SmartCompare(sourceField.Name));

                if (sourceProperty.Value != null)
                {
                    // Mapping when source field is enum type.
                    if (sourceField.Type.StartsWith(EnumIdentity))
                    {
                        var value = EnumMapping(sourceProperty.Value, sourceField.Type, targetType, enums);
                        targetEntity.SetValue(targetField.Name, value);
                        targetFieldInfos.Add(new EntityFieldInfo(targetField.Name, targetType));
                        continue;
                    }

                    // Mapping when target field is enum type.
                    if (targetField.Type.StartsWith(EnumIdentity))
                    {
                        var value = EnumMapping(sourceProperty.Value, targetField.Type, typeof(int), enums);
                        targetEntity.SetValue(targetField.Name, value);
                        targetFieldInfos.Add(new EntityFieldInfo(targetField.Name, targetType));
                        continue;
                    }

                    // Set value normal case.
                    targetEntity.SetValue(targetField.Name, Convert(sourceProperty.Value, targetType));
                    targetFieldInfos.Add(new EntityFieldInfo(targetField.Name, targetType));
                    continue;
                }

                // Handle null value case.
                if (sourceProperty.Key == null || isNew)
                    continue;

                targetEntity.SetValue(targetField.Name, null);
                targetFieldInfos.Add(new EntityFieldInfo(targetField.Name, targetType));
            }

            return targetFieldInfos;
        }

        /// <summary>
        /// Build target field value by specified converter.
        /// </summary>
        /// <param name="mapping">Current field mapping</param>
        /// <param name="enums">Enums mapping for mapping enum field.</param>
        /// <param name="targetAgent">Target agent for handle persistence logic.</param>
        /// <param name="targetField">Target field information</param>
        /// <param name="sourceEntity">Source entity for sync entity</param>
        /// <returns>Target field value</returns>
        private static object BuildTargetFieldValueByConvert(FieldMapping mapping, Enums enums, IAgent targetAgent,
            Field targetField, IDictionary<string, object> sourceEntity)
        {
            var builder = new ExpressionBuilder();
            var descriptor = builder.Build(targetField.Convert);
            descriptor.TargetFieldType = targetField.Type;
            var targetFieldType = Type.GetType(targetField.Type);

            // Get converter plugin by name.
            var converter = AutofacLocator.ResolveConverter<IConverterPlugin>(descriptor.MethodName);

            // Build target value by expression worker.
            var worker = new ExpressionWorker(descriptor);
            var targetValue = worker.Compute(converter, targetAgent, propertyName =>
            {
                var property = sourceEntity.FirstOrDefault(p => p.Key.SmartCompare(propertyName));
                if (property.Value == null)
                    return null;

                var field = mapping.SourceFields.Field.FirstOrDefault(f => f.Name.SmartCompare(propertyName));

                if (field == null || !field.Type.StartsWith(EnumIdentity))
                    return property.Value;


                var value = EnumMapping(property.Value, field.Type, targetFieldType, enums);
                return value.ToString();
            });
            return targetValue;
        }

        /// <summary>
        /// Enum mapping between int and enum value.
        /// </summary>
        /// <param name="value">Field value, posible int or enum.</param>
        /// <param name="enumName">Enum name for locate enum mapping</param>
        /// <param name="targetType">Target field type, posbile int or enum.</param>
        /// <param name="enums">Enums mapping in sync mapping</param>
        /// <returns>Enum value</returns>
        private static object EnumMapping(object value, string enumName, Type targetType, Enums enums)
        {
            if (enums == null)
            {
                return null;
            }

            var enumSections = enumName.Split('.');
            if (enumSections.Length != 2)
            {
                return null;
            }

            var enumKey = enumSections[1];
            var enumMapping = enums.Enum.FirstOrDefault(e => e.Name.Equals(enumKey));

            if (enumMapping == null
                || !enumMapping.Field.Any())
            {
                return null;
            }

            if (targetType == typeof(int)
                || targetType == null)
            {
                var index =
                    enumMapping.Field.FindIndex(
                        f =>
                            (value == null && f.Value == null) ||
                            (value != null && f.Value != null &&
                             f.Value.Equals(value.ToString(), StringComparison.OrdinalIgnoreCase)));
                if (index >= 0)
                {
                    return index;
                }
            }

            if (targetType == typeof(string) && value.GetType().IsPrimitive)
            {
                var index = (int)value;
                if (index < enumMapping.Field.Count)
                {
                    return enumMapping.Field[index].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Convert value by specified type.
        /// </summary>
        /// <param name="value">Object value</param>
        /// <param name="type">Type for convert</param>
        /// <returns>After converted value</returns>
        private object Convert(object value, Type type)
        {
            object result = null;
            try
            {
                result = System.Convert.ChangeType(value, type);
            }
            catch (Exception)
            {
                _log.Warn(String.Format("Source value({0}) cannot convert to target type({1})", value, type));
            }

            if (type == typeof(DateTime) && result != null)
            {
                return XmlConvert.ToString((DateTime)result);
            }

            return result;
        }

        #endregion
    }
}
