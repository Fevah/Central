using System.Collections.Generic;
using DevExpress.Data.Filtering;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;

namespace TIG.IntegrationServer.Plugin.Core.ServiceContext.Interface
{
    public interface IServiceContext
    {
        /// <summary>
        /// GetOne method by filter.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entity</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        EntityBase GetOne(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter,
            params EntityJoinInfo[] joinEntityInfos);

        /// <summary>
        /// GetAll method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entities</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        IEnumerable<EntityBase> GetAll(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter,
            params EntityJoinInfo[] joinEntityInfos);

        /// <summary>
        /// Update method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <param name="filter">Filter for locate required entity</param>
        bool Update(string entityName, List<EntityFieldInfo> fieldInfos, EntityBase entity, CriteriaOperator filter);

        /// <summary>
        /// Create entity by entity information.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <returns>Retrieved entity from persistence</returns>
        EntityBase Create(string entityName, List<EntityFieldInfo> fieldInfos, EntityBase entity);

        /// <summary>
        /// Delete entitys
        /// </summary>
        /// <param name="entityName">Entity name for delete</param>
        /// <param name="filter">Filter for delete entites</param>
        /// <returns>Bool, indicate delete success or not</returns>
        bool Delete(string entityName, CriteriaOperator filter);
    }
}