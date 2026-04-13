using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Core
{
    public abstract class TaskBuilderBase : SyncEngineContextConsumerComponent, ITaskBuilder
    {
        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="log">Log witter</param>
        /// <param name="context">Task context</param>
        protected TaskBuilderBase(ILog log, IContext context)
            : base(log, context)
        {
        }

        #endregion
    }
}
