using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.Interface;

namespace TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface
{
    public interface IAgentPlugin : IPlugin
    {
        /// <summary>
        /// GetAgent method for get agent service for different plugin.
        /// </summary>
        /// <returns></returns>
        IAgent BuildAgent();

        /// <summary>
        /// Change tracker configuration.
        /// </summary>
        ChangeTrackerConfigurationElement ChangeTrackerConfiguration { get; }
    }
}
