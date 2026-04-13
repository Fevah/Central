using System;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Task;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Core;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.TaskBuilders
{
    public class SyncEntityBundleTaskBuilder : TaskBuilderBase, ISyncEntityBundleTaskBuilder
    {
        #region Private Fields

        private readonly ISyncInstanceBundleTaskBuilder _syncInstanceBundleTaskBuilder;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="log">Log writer</param>
        /// <param name="context">Task context</param>
        /// <param name="syncInstanceBundleTaskBuilder">Sync instance bundle task builder</param>
        public SyncEntityBundleTaskBuilder(
            ILog log,
            IContext context,
            ISyncInstanceBundleTaskBuilder syncInstanceBundleTaskBuilder)
            : base(log, context)
        {
            _syncInstanceBundleTaskBuilder = syncInstanceBundleTaskBuilder;
        }

        #endregion


        #region ISyncEntityBundleTaskBuilder Methods

        /// <summary>
        /// Build sync task by sync entity bundle
        /// </summary>
        /// <param name="entityBundleId">Sync entity bundle id</param>
        /// <returns>Sync entity bundle task</returns>
        public ISyncEntityBundleTask BuildTask(Guid entityBundleId)
        {
            var syncTypeData = Context.Configuration.GetTaskData(entityBundleId);
            var syncTypeTask = new SyncEntityBundleTask(Log, Context, syncTypeData, _syncInstanceBundleTaskBuilder);
            return syncTypeTask;
        }

        #endregion
    }
}
