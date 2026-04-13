using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;

namespace TIG.IntegrationServer.Plugin.Agent.TotalLinkAgent.Entity
{
    public class TotalLinkEntity : EntityBase
    {
        #region Public Properties

        /// <summary>
        /// Entity unique identifier. Should be set by the agent. Sync engine can only read it.
        /// </summary>
        public override object Id
        {
            get { return base["Oid"]; }
        }

        #endregion
    }
}