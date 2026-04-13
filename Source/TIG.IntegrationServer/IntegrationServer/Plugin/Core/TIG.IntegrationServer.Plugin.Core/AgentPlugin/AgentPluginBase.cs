using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Core.AgentPlugin
{
    public abstract class AgentPluginBase : IAgentPlugin
    {
        /// <summary>
        /// GetAgent method for get agent service for different plugin.
        /// </summary>
        /// <returns></returns>
        public abstract IAgent BuildAgent();

        /// <summary>
        /// Change tracker configuration.
        /// </summary>
        public abstract ChangeTrackerConfigurationElement ChangeTrackerConfiguration { get; }
    }
}
