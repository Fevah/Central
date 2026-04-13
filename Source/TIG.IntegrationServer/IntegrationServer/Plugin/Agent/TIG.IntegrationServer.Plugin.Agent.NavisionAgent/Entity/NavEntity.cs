using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;

namespace TIG.IntegrationServer.Plugin.Agent.NavisionAgent.Entity
{
    public class NavEntity : EntityBase
    {
        #region Public Properties

        /// <summary>
        /// Entity unique identifier. Should be set by the agent. Sync engine can only read it.
        /// </summary>
        public override object Id
        {
            get { return base["No"]; }
        }

        #endregion
    }
}
