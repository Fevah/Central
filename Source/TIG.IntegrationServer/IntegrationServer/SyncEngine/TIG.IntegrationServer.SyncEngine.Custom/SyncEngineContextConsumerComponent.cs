using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;

namespace TIG.IntegrationServer.SyncEngine.Custom
{
    public abstract class SyncEngineContextConsumerComponent : SyncEngineComponent
    {
        #region Protected Fields

        protected readonly IContext Context;

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor with components
        /// </summary>
        /// <param name="log">Log writter</param>
        /// <param name="context">Sync context</param>
        protected SyncEngineContextConsumerComponent(ILog log, IContext context)
            :base(log)
        {
            Context = context;
        }

        #endregion
    }
}
