using System;

namespace TIG.IntegrationServer.SyncEngine.Core.Interface
{
    public interface ISyncEntityStatus
    {
        #region Properties

        /// <summary>
        /// Sync entity key
        /// </summary>
        string EntityKey { get; set; }

        /// <summary>
        /// SourceAgentId for identify source agent
        /// </summary>
        string SourceAgentId { get; set; }

        /// <summary>
        /// TargetAgentId for identify target agent
        /// </summary>
        string TargetAgentId { get; set; }

        /// <summary>
        /// SynceTime for record when sync happen
        /// </summary>
        DateTime SynceTime { get; set; }

        #endregion

    }
}