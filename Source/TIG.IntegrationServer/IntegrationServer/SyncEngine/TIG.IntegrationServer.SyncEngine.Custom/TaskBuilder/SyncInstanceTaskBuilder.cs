using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Core;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.TaskBuilders
{
    public class SyncInstanceTaskBuilder : TaskBuilderBase, ISyncInstanceTaskBuilder
    {
        #region Constructors

        /// <summary>
        /// Default Constructors
        /// </summary>
        /// <param name="log">Log writer</param>
        /// <param name="context">Task context</param>
        public SyncInstanceTaskBuilder(
            ILog log,
            IContext context)
            : base(log, context)
        {
        }

        #endregion


        #region ISyncInstanceTaskBuilder Members

        /// <summary>
        /// Build sync instance task.
        /// </summary>
        /// <param name="syncInstance">Sync instance for sync</param>
        /// <param name="map">Sync entity map for sync entity</param>
        /// <param name="taskeData">Sync task data</param>
        /// <returns>Sync instance task</returns>
        public ISyncInstanceTask BuildTask(SyncInstance syncInstance, SyncEntityMap map, ITaskData taskeData)
        {
            var syncItemDetailTask = new SyncInstanceTask(Log, Context, taskeData, syncInstance, map);
            return syncItemDetailTask;
        }

        #endregion
    }
}
