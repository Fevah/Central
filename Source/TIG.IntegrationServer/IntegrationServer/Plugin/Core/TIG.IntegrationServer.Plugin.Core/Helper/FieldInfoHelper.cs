using System;
using System.Collections.Generic;
using System.Linq;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.Plugin.Core.Helper
{
    public class FieldInfoHelper
    {
        #region Public Methods

        /// <summary>
        /// Get fieldInfos
        /// </summary>
        /// <param name="entity">Entity for build field info</param>
        /// <param name="entityMap">Entity mapping of current entity</param>
        /// <returns>Field infos use for query in cached data service.</returns>
        public static List<EntityFieldInfo> GetFieldInfos(IEntity entity, SyncEntityMap entityMap)
        {
            // Get entity mappings
            var syncMapping = entityMap.GetSyncMapping();
            var mappings =
                syncMapping.EntityMappings.EntityMapping.Where(em => em.Name == entityMap.SourceEntity.EntityName);

            // Get field names to build fieldInfos.
            var targetFieldNames = entity.EntityPropertyNames();

            // Get all fieldInfos from mappings
            var fieldInfos = from entityMapping in mappings
                             from fieldMapping in entityMapping.FieldMapping
                             from targetField in fieldMapping.TargetFields.Field
                             where targetFieldNames.Contains(targetField.Name)
                             select targetField;

            // Build fieldInfos based on fieldInfos of mappings.
            return (from fieldName in targetFieldNames
                    let fieldInfo = fieldInfos.FirstOrDefault(p => p.Name == fieldName)
                    where fieldInfo != null
                    select new EntityFieldInfo(fieldName, Type.GetType(fieldInfo.Type))).ToList();
        }

        /// <summary>
        /// Get fieldinfos with joinInfo
        /// </summary>
        /// <param name="entityMap">Entity mapping of current entity</param>
        /// <param name="fieldInfo">Field infos use for query in cached data service.</param>
        /// <param name="joinEntityInfo">Join entity infos use for query in cached data service.</param>
        public static void GetFieldInfosWithJoinInfo(SyncEntityMap entityMap, List<EntityFieldInfo> fieldInfo,
            List<EntityJoinInfo> joinEntityInfo)
        {
            if (fieldInfo == null)
            {
                throw new ArgumentNullException("fieldInfo", "fieldInfo list cannot be null.");
            }

            if (joinEntityInfo == null)
            {
                throw new ArgumentNullException("joinEntityInfo", "joinEntityInfo list cannot be null.");
            }

            // Get entity mappings
            var syncMapping = entityMap.GetSyncMapping();
            var mappings =
                syncMapping.EntityMappings.EntityMapping.Where(em => em.Name == entityMap.SourceEntity.EntityName);

            // Build fieldinfo from mappings
            foreach (var fieldMapping in from mapping in mappings
                                         from fieldMapping in mapping.FieldMapping
                                         select fieldMapping)
            {
                if (fieldMapping.SourceFields == null)
                {
                    continue;
                }

                foreach (var sourceField in fieldMapping.SourceFields.Field)
                {
                    // Build JoinEntityInfo, if there any object type.
                    if (!sourceField.Type.StartsWith("System.")
                        && !sourceField.Type.StartsWith("Enum.")
                        && joinEntityInfo.All(p => p.EntityName != sourceField.Name))
                    {
                        // Build join information.
                        var entityInfo = new EntityJoinInfo
                        {
                            EntityName = sourceField.Name,
                            EntityType = sourceField.Type,
                            JoinKey = "Oid",
                            FieldInfos = new List<EntityFieldInfo>()
                        };

                        // Get field information from expression.
                        foreach (
                            var field in fieldMapping.TargetFields.Field.Where(p => !string.IsNullOrEmpty(p.Convert)
                                                                                    &&
                                                                                    p.Convert.Contains(sourceField.Name))
                            )
                        {
                            entityInfo.FieldInfos.Add(new EntityFieldInfo
                            {
                                // Convert="Property([OwnershipType], Name, System.String)"
                                Name = field.Convert.Split(',')[1].Trim(),
                                Type = Type.GetType(field.Type)
                            });
                        }

                        joinEntityInfo.Add(entityInfo);
                    }

                    // Build field info
                    if (fieldInfo.All(f => f.Name != sourceField.Name))
                    {
                        fieldInfo.Add(new EntityFieldInfo
                        {
                            Name = sourceField.Name,
                            Type = Type.GetType(sourceField.Type)
                        });
                    }
                }
            }
        }

        #endregion
    }
}