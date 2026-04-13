using System.Collections.Generic;
using DevExpress.Data.Filtering;
using TIG.IntegrationServer.Plugin.Agent.NavisionAgent.Entity;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.Plugin.Core.ServiceContext.Interface;

namespace TIG.IntegrationServer.Plugin.Agent.NavisionAgent.Agent
{
    public class NavAgent : AgentBase
    {
        #region Private Properties

        private readonly IServiceContext _context;

        #endregion


        #region Constructors

        /// <summary>
        /// Construct with service context
        /// </summary>
        /// <param name="context">OData service context</param>
        public NavAgent(IServiceContext context)
        {
            _context = context;
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Build empty ecommerce entity
        /// </summary>
        /// <returns>Entity for return to invoker</returns>
        public override IEntity BuildEmptyEntity()
        {
            return new NavEntity();
        }

        /// <summary>
        /// Create entity by entity information.
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <returns>Retrieved entity from persistence</returns>
        public override IEntity Create(string entityName, List<EntityFieldInfo> fieldInfos, IEntity entity)
        {
            return _context.Create(entityName, fieldInfos, entity as EntityBase);
        }

        /// <summary>
        /// Update method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="entity">Entity object for create a new entity.</param>
        /// <param name="filter">Filter for locate required entity</param>
        public override void Update(string entityName, List<EntityFieldInfo> fieldInfos, IEntity entity,
            CriteriaOperator filter)
        {
            _context.Update(entityName, fieldInfos, entity as EntityBase, filter);
        }

        /// <summary>
        /// ReadAll method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entities</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public override IEnumerable<IEntity> ReadAll(string entityName, List<EntityFieldInfo> fieldInfos,
            CriteriaOperator filter, params EntityJoinInfo[] joinEntityInfos)
        {
            return _context.GetAll(entityName, fieldInfos, filter, joinEntityInfos);
        }

        /// <summary>
        /// ReadOne method by filter
        /// </summary>
        /// <param name="entityName">Entity name for create a new entity</param>
        /// <param name="fieldInfos">Field information of entity</param>
        /// <param name="filter">Filter for locate required entity</param>
        /// <param name="joinEntityInfos">Extra entities information</param>
        /// <returns>Entity retrieved from persistence</returns>
        public override IEntity ReadOne(string entityName, List<EntityFieldInfo> fieldInfos, CriteriaOperator filter,
            params EntityJoinInfo[] joinEntityInfos)
        {
            return _context.GetOne(entityName, fieldInfos, filter, joinEntityInfos);
        }

        #endregion
    }
}
