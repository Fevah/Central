using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin;

namespace TIG.IntegrationServer.Plugin.Agent.ECommerceAgent.Core
{
    public abstract class EcommerceAgentPluginBase : AgentPluginBase
    {
        #region Protected Properties

        protected SyncAgentConfigurationSection Configuration { get; private set; }

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        protected EcommerceAgentPluginBase()
        {
            // Get configuration.
            Configuration = ConfigurationHelper.GetPrivateConfiguration<SyncAgentConfigurationSection>(GetType().Assembly.Location,
                    "syncAgentConfigurationSection");
        }

        #endregion

    }
}
