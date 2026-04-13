using System;
using System.Collections.Generic;
using DevExpress.Data.Filtering;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;

namespace TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent
{
    public interface IAgent : IDisposable
    {
        /// <summary>
        /// Create a empty entity.
        /// </summary>
        /// <returns>Return entity created by this method.</returns>
        IEntity BuildEmptyEntity();

        /// <summary>
        /// Save entity by entity information.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <returns>Retrieved entity from persistence</returns>
        IEntity Create(string entityName, List<EntityFieldInfo> fieldInfos, IEntity entity);

        /// <summary>
        /// ReadOne method by filter.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entity</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        IEntity ReadOne(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos);

        /// <summary>
        /// ReadAll method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entities</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        IEnumerable<IEntity> ReadAll(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos);

        /// <summary>
        /// Update method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <param name="filter">Filter for locate required entity</param>
        void Update(string entityName, List<EntityFieldInfo> fieldInfos, IEntity entity, CriteriaOperator filter);
    }
}
