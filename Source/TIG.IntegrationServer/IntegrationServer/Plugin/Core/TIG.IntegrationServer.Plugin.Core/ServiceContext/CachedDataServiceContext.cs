using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Xml;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using TIG.IntegrationServer.Common;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Constant;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.ServiceContext.Interface;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientBehavior;
using TIG.TotalLink.Shared.Facade.Core.ServiceClientMessageInspector;

namespace TIG.IntegrationServer.Plugin.Core.ServiceContext
{
    public class CachedDataServiceContext : IServiceContext
    {
        #region Private Properties

        public readonly CachedDataStoreClient Client;
        private Random _gcRecordIdGenerator;
        private ILog Log;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor cached data service context
        /// </summary>
        /// <param name="address">Service address</param>
        /// <param name="authenticationToken">Authentication token to login data service</param>
        public CachedDataServiceContext(string address, string authenticationToken)
        {
            // Create an endpoint and binding for the service
            var endpoint = new EndpointAddress(address);
            var binding = new BasicHttpBinding
            {
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferSize = int.MaxValue,
                TransferMode = TransferMode.Streamed,
                OpenTimeout = new TimeSpan(0, 5, 0),
                CloseTimeout = new TimeSpan(0, 5, 0),
                SendTimeout = new TimeSpan(0, 5, 0),
                ReceiveTimeout = new TimeSpan(0, 5, 0),
                ReaderQuotas = new XmlDictionaryReaderQuotas
                {
                    MaxDepth = int.MaxValue,
                    MaxArrayLength = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                }
            };

            // Create a cached client
            Client = new CachedDataStoreClient(binding, endpoint);

            // If an authentication token was specified, add an authentication inspector to the channel
            if (string.IsNullOrEmpty(authenticationToken))
                return;

            var inspector = new AuthenticationClientMessageInspector(authenticationToken);
            Client.Endpoint.EndpointBehaviors.Add(new AuthenticationEndpointClientBehavior(inspector));

            Log = AutofacLocator.Resolve<ILog>();

            if (Log == null)
            {
                throw new ArgumentNullException("Log", "Please make sure loger componet was loaded.");
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// GetOne method by filter.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entity</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public EntityBase GetOne(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter,
            params EntityJoinInfo[] joinEntityInfos)
        {
            CriteriaOperator condtion;
            if (!entityName.Equals("XPObjectType"))
            {
                // Add GC filter
                condtion = new UnaryOperator(UnaryOperatorType.IsNull,
                    new QueryOperand("GCRecord", EntityAlias.DefaultEntityAlias, DBColumnType.Int32));

                if (filter != null)
                {
                    condtion = CriteriaOperator.And(condtion, filter);
                }
            }
            else
            {
                condtion = filter;
            }

            try
            {
                var channel = Client.ChannelFactory.CreateChannel();
                var selectStatement = new SelectStatement
                {
                    TableName = entityName,
                    TopSelectedRecords = 1,
                    Alias = EntityAlias.DefaultEntityAlias,
                    Condition = condtion
                };

                // Load extra entities.
                if (joinEntityInfos != null && joinEntityInfos.Any())
                {
                    var index = 1;
                    const string aliasFormat = "N{0}";
                    foreach (var entityInfo in joinEntityInfos)
                    {
                        var fieldInfo = fieldInfos.FirstOrDefault(p => p.Name.Equals(entityInfo.EntityName));
                        if (fieldInfo == null)
                            continue;

                        var alias = string.Format(aliasFormat, index);
                        entityInfo.Alias = alias;

                        var joinNode = new JoinNode
                        {
                            Alias = alias,
                            Condition =
                                new BinaryOperator(new QueryOperand(entityInfo.EntityName, EntityAlias.DefaultEntityAlias),
                                    new QueryOperand(entityInfo.JoinKey, alias), BinaryOperatorType.Equal),
                            TableName = entityInfo.EntityName,
                            Type = JoinType.LeftOuter
                        };
                        selectStatement.SubNodes.Add(joinNode);

                        foreach (var info in entityInfo.FieldInfos)
                        {
                            selectStatement.Operands.Add(new QueryOperand(info.Name, alias,
                                DBColumn.GetColumnType(info.Type)));
                        }

                        index++;
                    }
                }

                // Add default key to system.
                if (fieldInfos == null)
                {
                    fieldInfos = new List<EntityFieldInfo>();
                }

                if (fieldInfos.All(q => q.Name != "Oid"))
                {
                    fieldInfos.Insert(0, new EntityFieldInfo("Oid", entityName.Equals("XPObjectType") ? typeof(int) : typeof(Guid)));
                }

                var queryOperands =
                    fieldInfos.Select(
                        fi => new QueryOperand(fi.Name, EntityAlias.DefaultEntityAlias, DBColumn.GetColumnType(fi.Type)));
                selectStatement.Operands.AddRange(queryOperands);

                // Automatic get OptimisticLockField FieldInfo.
                if (joinEntityInfos == null || !joinEntityInfos.Any())
                {
                    if (!entityName.Equals("XPObjectType"))
                    {
                        var queryOperand = new QueryOperand("OptimisticLockField", EntityAlias.DefaultEntityAlias,
                            DBColumnType.Int32);
                        selectStatement.Operands.Add(queryOperand);
                        if (fieldInfos.All(p => p.Name != "OptimisticLockField"))
                            fieldInfos.Add(new EntityFieldInfo { Name = "OptimisticLockField", Type = typeof(int) });
                    }
                }

                var data = channel.SelectData(new[] { selectStatement });
                var selectStatementResult = data.Result.ResultSet.FirstOrDefault();
                if (selectStatementResult == null)
                    return null;

                // Build entity
                var entity = selectStatementResult.Rows.Select(row =>
                {
                    if (joinEntityInfos != null && joinEntityInfos.Any())
                    {
                        var values =
                            row.Values.Skip(joinEntityInfos.Where(p => p.Alias != null).Sum(p => p.FieldInfos.Count));
                        var dic =
                            values.Zip(fieldInfos.Select(p => p.Name), (v, k) => new { v, k })
                                .ToDictionary(item => item.k, item => item.v);
                        var dataEntity = EntityBase.Map(dic);
                        var seed = 0;
                        foreach (var joinEntityInfo in joinEntityInfos.Where(p => p.Alias != null))
                        {
                            var count = joinEntityInfo.FieldInfos.Count;
                            values = row.Values.Skip(seed).Take(count);
                            seed += count;
                            dic = values.Zip(joinEntityInfo.FieldInfos.Select(p => p.Name), (v, k) => new { v, k })
                                    .ToDictionary(item => item.k, item => item.v);
                            var subEntity = EntityBase.Map(dic);
                            dataEntity[joinEntityInfo.EntityName] = subEntity;
                        }

                        return dataEntity;
                    }

                    var obj =
                        row.Values.Zip(fieldInfos.Select(p => p.Name), (v, k) => new { v, k })
                            .ToDictionary(item => item.k, item => item.v);
                    return EntityBase.Map(obj);
                }).FirstOrDefault();

                return entity;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Request: ({0}({1})), Message: {2}",
                    entityName,
                    condtion,
                    ex.Message));
                return null;
            }
        }

        /// <summary>
        /// GetAll method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entities</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public IEnumerable<EntityBase> GetAll(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter,
            params EntityJoinInfo[] joinEntityInfos)
        {
            CriteriaOperator condtion;
            if (!entityName.Equals("XPObjectType"))
            {
                // Add GC filter
                condtion = new UnaryOperator(UnaryOperatorType.IsNull,
                    new QueryOperand("GCRecord", EntityAlias.DefaultEntityAlias, DBColumnType.Int32));

                if (filter != null)
                {
                    condtion = CriteriaOperator.And(condtion, filter);
                }
            }
            else
            {
                condtion = filter;
            }

            try
            {

                var channel = Client.ChannelFactory.CreateChannel();
                var selectStatement = new SelectStatement
                {
                    TableName = entityName,
                    Alias = EntityAlias.DefaultEntityAlias,
                    Condition = condtion
                };

                // Load extra entities.
                if (joinEntityInfos != null && joinEntityInfos.Any())
                {
                    var index = 1;
                    const string aliasFormat = "N{0}";
                    foreach (var entityInfo in joinEntityInfos)
                    {
                        var fieldInfo = fieldInfos.FirstOrDefault(p => p.Name.Equals(entityInfo.EntityName));
                        if (fieldInfo == null)
                            continue;

                        var alias = string.Format(aliasFormat, index);
                        entityInfo.Alias = alias;

                        var joinNode = new JoinNode
                        {
                            Alias = alias,
                            Condition =
                                new BinaryOperator(new QueryOperand(entityInfo.EntityName, EntityAlias.DefaultEntityAlias),
                                    new QueryOperand(entityInfo.JoinKey, alias), BinaryOperatorType.Equal),
                            TableName = entityInfo.EntityType,
                            Type = JoinType.LeftOuter
                        };
                        selectStatement.SubNodes.Add(joinNode);

                        foreach (var info in entityInfo.FieldInfos)
                        {
                            selectStatement.Operands.Add(new QueryOperand(info.Name, alias,
                                DBColumn.GetColumnType(info.Type)));
                        }

                        index++;
                    }
                }

                if (fieldInfos == null)
                {
                    fieldInfos = new List<EntityFieldInfo>();
                }

                if (fieldInfos.All(q => q.Name != "Oid"))
                {
                    fieldInfos.Insert(0, new EntityFieldInfo("Oid", entityName.Equals("XPObjectType") ? typeof(int) : typeof(Guid)));
                }

                var usefulFieldInfos = fieldInfos.Where(p => p.Type != null).ToList();

                var queryOperands = usefulFieldInfos.Select(fieldInfo =>
                                new QueryOperand(fieldInfo.Name, EntityAlias.DefaultEntityAlias,
                                    DBColumn.GetColumnType(fieldInfo.Type)));

                selectStatement.Operands.AddRange(queryOperands);

                // Automatic get OptimisticLockField FieldInfo.
                if (joinEntityInfos == null || !joinEntityInfos.Any())
                {
                    if (!entityName.Equals("XPObjectType"))
                    {
                        var queryOperand = new QueryOperand("OptimisticLockField", EntityAlias.DefaultEntityAlias,
                            DBColumnType.Int32);
                        selectStatement.Operands.Add(queryOperand);
                        if (fieldInfos.All(p => p.Name != "OptimisticLockField"))
                            fieldInfos.Add(new EntityFieldInfo { Name = "OptimisticLockField", Type = typeof(int) });
                    }
                }

                var data = channel.SelectData(new[] { selectStatement });
                var selectStatementResult = data.Result.ResultSet.FirstOrDefault();
                if (selectStatementResult == null)
                    return null;

                // Build entity.
                var entites = selectStatementResult.Rows.Select(row =>
                {
                    if (joinEntityInfos != null && joinEntityInfos.Any())
                    {
                        var skip = joinEntityInfos.Where(p => p.Alias != null).Sum(p => p.FieldInfos.Count);

                        var values = row.Values.Skip(skip);
                        var dic = values.Zip(usefulFieldInfos.Select(p => p.Name), (v, k) => new { v, k })
                                .ToDictionary(item => item.k, item => item.v);
                        var entity = EntityBase.Map(dic);
                        var seed = 0;
                        foreach (var joinEntityInfo in joinEntityInfos.Where(p => p.Alias != null))
                        {
                            var count = joinEntityInfo.FieldInfos.Count;
                            values = row.Values.Skip(seed).Take(count);
                            seed += count;
                            dic = values.Zip(joinEntityInfo.FieldInfos.Select(p => p.Name), (v, k) => new { v, k })
                                    .ToDictionary(item => item.k, item => item.v);
                            var subEntity = EntityBase.Map(dic);
                            entity[joinEntityInfo.EntityName] = subEntity;
                        }

                        return entity;
                    }

                    var obj =
                        row.Values.Zip(usefulFieldInfos.Select(p => p.Name), (v, k) => new { v, k })
                            .ToDictionary(item => item.k, item => item.v);
                    return EntityBase.Map(obj);
                });

                return entites;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Request: ({0}({1})), Message: {2}",
                            entityName,
                            condtion,
                            ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Update method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <param name="filter">Filter for locate required entity</param>
        public bool Update(string entityName, List<EntityFieldInfo> fieldInfos, EntityBase entity, CriteriaOperator filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException("filter");
            }

            CriteriaOperator condition = null;
            try
            {
                // Add OptimisticLockField, if it is exist in current entity.
                if (entity.ContainsKey("OptimisticLockField") && entity["OptimisticLockField"] != null)
                {
                    condition = new BinaryOperator(new QueryOperand("OptimisticLockField", null, DBColumnType.Int32),
                        new OperandValue((int)entity["OptimisticLockField"]), BinaryOperatorType.Equal);
                }

                condition = condition != null ? CriteriaOperator.And(condition, filter) : filter;

                var channel = Client.ChannelFactory.CreateChannel();
                var modifyStatement = new UpdateStatement
                {
                    TableName = entityName,
                    Alias = EntityAlias.DefaultEntityAlias,
                    Condition = condition,
                    RecordsAffected = 1
                };

                foreach (var fieldInfo in fieldInfos)
                {
                    // Object type
                    if (fieldInfo.Type == null)
                    {
                        var fieldObject = entity[fieldInfo.Name] as IEntity;
                        if (fieldObject == null)
                        {
                            continue;
                        }

                        var key = fieldObject.GetValue(EntityAlias.PrimaryKey);

                        if (key == null)
                        {
                            continue;
                        }

                        entity[fieldInfo.Name] = key;
                        fieldInfo.Type = typeof(Guid);
                    }

                    var operand = new QueryOperand(fieldInfo.Name, null, DBColumn.GetColumnType(fieldInfo.Type));
                    modifyStatement.Operands.Add(operand);
                    modifyStatement.Parameters.Add(new OperandValue(entity[fieldInfo.Name]));
                }

                var data = channel.ModifyData(new ModificationStatement[] { modifyStatement });
                return data.Error == null;
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Request: ({0}({1})), Message: {2}",
                            entityName,
                            condition,
                            ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Create entity by entity information.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <returns>Retrieved entity from persistence</returns>
        public EntityBase Create(string entityName, List<EntityFieldInfo> fieldInfos, EntityBase entity)
        {
            try
            {
                var channel = Client.ChannelFactory.CreateChannel();
                var modifyStatement = new InsertStatement
                {
                    TableName = entityName
                };

                // Add OptimisticLockField, if it is exist in current fieldInfos.
                if (fieldInfos.All(p => p.Name != "OptimisticLockField"))
                    fieldInfos.Add(new EntityFieldInfo { Name = "OptimisticLockField", Type = typeof(int) });

                // Add OptimisticLockField, if it exist in current entity.
                if (!entity.ContainsKey("OptimisticLockField"))
                    entity.Add("OptimisticLockField", 0);

                // Add field infos to modifyStatement.
                foreach (var fieldInfo in fieldInfos)
                {
                    // Object type
                    if (fieldInfo.Type == null)
                    {
                        var fieldObject = entity[fieldInfo.Name] as IEntity;
                        if (fieldObject == null)
                        {
                            continue;
                        }

                        var key = fieldObject.GetValue(EntityAlias.PrimaryKey);

                        if (key == null)
                        {
                            continue;
                        }

                        entity[fieldInfo.Name] = key;
                        fieldInfo.Type = typeof(Guid);
                    }

                    var operand = new QueryOperand(fieldInfo.Name, null, DBColumn.GetColumnType(fieldInfo.Type));
                    modifyStatement.Operands.Add(operand);
                    modifyStatement.Parameters.Add(new OperandValue(entity[fieldInfo.Name]));
                }

                var data = channel.ModifyData(new ModificationStatement[] { modifyStatement });
                if (data.Error != null)
                {
                    Console.WriteLine("[{0}] Request: ({1}), Message: {2}",
                        DateTime.Now.ToString("g"),
                        entityName,
                        data.Error);
                    return null;
                }

                if (!data.Result.Identities.Any())
                    return entity;
                var dic = data.Result.Identities.Zip(fieldInfos.Select(p => p.Name), (v, k) => new { v.Value, k })
                        .ToDictionary(item => item.k, item => item.Value);

                return EntityBase.Map(dic);
            }
            catch (Exception ex)
            {
                Log.Error(string.Format("Request: ({0}), Message: {1}",
                        entityName,
                        ex.Message));

                return null;
            }
        }

        /// <summary>
        /// Delete entitys
        /// </summary>
        /// <param name="entityName">Entity name for delete</param>
        /// <param name="filter">Filter for delete entites</param>
        /// <returns></returns>
        public bool Delete(string entityName, CriteriaOperator filter)
        {
            // Add GCRecord field info
            var fieldInfos = new List<EntityFieldInfo>
            {
                new EntityFieldInfo {Name = "GCRecord", Type = typeof (int)}
            };

            // Generate GC value
            if (_gcRecordIdGenerator == null)
                _gcRecordIdGenerator = new Random();

            // Build entiy with GC field.
            var entity = new EntityBase();
            entity["GCRecord"] = _gcRecordIdGenerator.Next(1, int.MaxValue);

            return Update(entityName, fieldInfos, entity, filter);
        }

        #endregion
    }
}