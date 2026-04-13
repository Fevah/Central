using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin;

namespace TIG.IntegrationServer.Plugin.Agent.NavisionAgent.Core
{
    public abstract class NavAgentPluginBase : AgentPluginBase
    {
        #region Protected Properties

        protected SyncAgentConfigurationSection Configuration { get; private set; }

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        protected NavAgentPluginBase()
        {
            Configuration = ConfigurationHelper.GetPrivateConfiguration<SyncAgentConfigurationSection>(GetType().Assembly.Location, "syncAgentConfigurationSection");
        }

        #endregion
    }
}
