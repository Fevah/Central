using System.Linq;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Integration;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task
{
    public class SyncInstanceBundleTask : ParallelMultiTasksBase<ISyncInstanceTask>, ISyncInstanceBundleTask
    {
        #region Private Fields

        private readonly ISyncInstanceTaskBuilder _subTaskBuilder;
        private readonly SyncInstanceBundle _instanceBundle;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with components
        /// </summary>
        /// <param name="log">Log writer</param>
        /// <param name="context">Task context</param>
        /// <param name="taskData">Task data for hand persistence logic</param>
        /// <param name="instanceBundle">Sync instance bundle</param>
        /// <param name="subTaskBuilder">Sub task builder</param>
        public SyncInstanceBundleTask(
            ILog log,
            IContext context,
            ITaskData taskData,
            SyncInstanceBundle instanceBundle,
            ISyncInstanceTaskBuilder subTaskBuilder)
            : base(log, context, taskData, context.Configuration.ConcurrentSyncInstanceTasksPerSyncInstanceBundleTaskLimit)
        {
            _instanceBundle = instanceBundle;
            _subTaskBuilder = subTaskBuilder;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Prepare sub tasks
        /// </summary>
        protected override void PrepareSubTasks()
        {
            PrepareSyncInstances();

            foreach (var syncInstance in _instanceBundle.Instances)
            {
                // Only run high priority task first.
                if (syncInstance.State != SyncInstanceState.ChangeUnprocessed)
                {
                    continue;
                }

                // Get sync entity maps by sync entity
                var syncEntityMaps = TaskData.GetSyncEntityMap(syncInstance.Entity);

                // Buid sync entity task based on sync entity map
                foreach (var syncEntityMap in syncEntityMaps)
                {
                    // Check last sync time, make sure current sync is not back flow,
                    // If it is back flow, system will stop sync current entity.
                    var lastSyncTime = Context.SyncStatusManager.GetLastSyncTime(syncInstance.EntityRawId,
                        syncEntityMap.TargetEntity.AgentPluginId,
                        syncEntityMap.SourceEntity.AgentPluginId);

#if DEBUG && !TEST
                    if (lastSyncTime != null)
#else
                    var currentTime = System.DateTime.Now;
                    if (lastSyncTime != null
                        && lastSyncTime.Value.AddMinutes(20) > currentTime)
#endif
                    {
                        TaskData.MarkAsSynced(syncInstance);
                        continue;
                    }

                    var task = _subTaskBuilder.BuildTask(syncInstance, syncEntityMap, TaskData);
                    SubTasks.Add(task);
                }
            }
        }

        /// <summary>
        /// OnAllSubTasksFinished to hand when sub task all finished
        /// </summary>
        protected override void OnAllSubTasksFinished()
        {
            base.OnAllSubTasksFinished();

            if (SubTasks.All(i => i.State == TaskState.Completed))
            {
                TaskData.MarkAsSynced(_instanceBundle);
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Prepare sync instances by missing unsynced instances
        /// </summary>
        private void PrepareSyncInstances()
        {
            TaskData.CreateMissingUnsyncedInstances(_instanceBundle);
        }

        #endregion
    }
}
