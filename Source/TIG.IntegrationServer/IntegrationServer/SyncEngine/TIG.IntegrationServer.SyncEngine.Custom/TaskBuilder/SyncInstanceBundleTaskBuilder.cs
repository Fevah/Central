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
    public class SyncInstanceBundleTaskBuilder : TaskBuilderBase, ISyncInstanceBundleTaskBuilder
    {
        #region Private Methods

        private readonly ISyncInstanceTaskBuilder _syncInstanceTaskBuilder;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructors
        /// </summary>
        /// <param name="log">Log writer</param>
        /// <param name="context">Task context</param>
        /// <param name="syncInstanceTaskBuilder">sync Instance task builder</param>
        public SyncInstanceBundleTaskBuilder(
            ILog log,
            IContext context,
            ISyncInstanceTaskBuilder syncInstanceTaskBuilder)
            : base(log, context)
        {
            _syncInstanceTaskBuilder = syncInstanceTaskBuilder;
        }

        #endregion


        #region ISyncInstanceBundleTaskBuilder Members

        /// <summary>
        /// Build sync instance bundle task
        /// </summary>
        /// <param name="instanceBundle">Sync instance bundle</param>
        /// <param name="taskData">task data</param>
        /// <returns>Sync instance bundle task</returns>
        public ISyncInstanceBundleTask BuildTask(SyncInstanceBundle instanceBundle, ITaskData taskData)
        {
            var syncTypeTask = new SyncInstanceBundleTask(Log, Context, taskData, instanceBundle, _syncInstanceTaskBuilder);
            return syncTypeTask;
        }

        #endregion


    }
}
