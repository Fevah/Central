using System.Collections.Generic;
using DevExpress.Data.Filtering;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;

namespace TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent
{
    public abstract class AgentBase : IAgent
    {
        /// <summary>
        /// Create a empty entity.
        /// </summary>
        /// <returns>Return entity created by this method.</returns>
        public abstract IEntity BuildEmptyEntity();

        /// <summary>
        /// Save entity by entity information.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <returns>Retrieved entity from persistence</returns>
        public abstract IEntity Create(string entityName, List<EntityFieldInfo> fieldInfos, IEntity entity);

        /// <summary>
        /// ReadOne method by filter.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entity</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public abstract IEntity ReadOne(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos);

        /// <summary>
        /// ReadAll method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entities</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public abstract IEnumerable<IEntity> ReadAll(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos);

        /// <summary>
        /// Update method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <param name="filter">Filter for locate required entity</param>
        public abstract void Update(string entityName, List<EntityFieldInfo> fieldInfos, IEntity entity, CriteriaOperator filter);

        public void Dispose()
        {
        }
    }
}
