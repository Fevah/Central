using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Agent.NavisionAgent.Agent;
using TIG.IntegrationServer.Plugin.Agent.NavisionAgent.Core;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.ServiceContext;
using NetworkCredential = System.Net.NetworkCredential;

namespace TIG.IntegrationServer.Plugin.Agent.NavisionAgent
{
    public class NavAgentPlugin : NavAgentPluginBase
    {
        #region Overrides

        /// <summary>
        /// Build Nav agent.
        /// </summary>
        /// <returns>Retrieved Nav agent</returns>
        public override IAgent BuildAgent()
        {
            var entityServiceSettings = Configuration.SyncServiceSettings;
            var credentialSettings = entityServiceSettings.NetworkCredential;

            var netCred = new NetworkCredential(credentialSettings.Username, credentialSettings.Password,
                credentialSettings.Domain);

            var context = new ODataServiceContext(entityServiceSettings.ServiceUri, null, netCred,
                entityServiceSettings.MetadataUri);

            var agent = new NavAgent(context);
            return agent;
        }

        /// <summary>
        /// ChangeTrackerConfiguration for change tracker service specification
        /// </summary>
        public override ChangeTrackerConfigurationElement ChangeTrackerConfiguration
        {
            get { return Configuration.ChangeTrackerSettings; }
        }

        #endregion
    }
}
