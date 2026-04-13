using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.Plugin.Core.MapperPlugin.Interface
{
    public interface IFieldMapperPlugin : IMapperPlugin
    {
        /// <summary>
        /// Map method for mapping source object to target object.
        /// </summary>
        /// <param name="syncEntityMap">Entity mapping for mapping entity.</param>
        /// <param name="sourceEntity">Source entity for mapping</param>
        /// <param name="targetEntity">Target entity for mapping</param>
        /// <param name="sourceAgent">Source agent to handle persistence operate.</param>
        /// <param name="targetAgent">Target agent to handle persistence operate.</param>
        /// <param name="isNew">Indicate current mapping object is new or not.</param>
        void Map(SyncEntityMap syncEntityMap, IEntity sourceEntity, IEntity targetEntity, IAgent sourceAgent, IAgent targetAgent, bool isNew);
    }
}
