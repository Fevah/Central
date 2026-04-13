using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using TIG.IntegrationServer.Common;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Agent;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Entity;
using TIG.IntegrationServer.Plugin.Core.AgentPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Enum;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Interface;
using TIG.IntegrationServer.Plugin.Core.Constant;
using TIG.IntegrationServer.Plugin.Core.Entity;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Extension;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task
{
    public class SyncEntityBundleTask : ParallelMultiTasksBase<ISyncInstanceBundleTask>, ISyncEntityBundleTask
    {
        #region Private Fields

        private readonly ISyncInstanceBundleTaskBuilder _subTaskBuilder;

        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with components
        /// </summary>
        /// <param name="log">Log writer</param>
        /// <param name="context">Task context</param>
        /// <param name="taskData">Task data for hand persistence logic</param>
        /// <param name="subTaskBuilder">Sub task builder</param>
        public SyncEntityBundleTask(
            ILog log,
            IContext context,
            ITaskData taskData,
            ISyncInstanceBundleTaskBuilder subTaskBuilder)
            : base(log, context, taskData, context.Configuration.ConcurrentSyncInstanceBundleTasksPerSyncEntityBundleTaskLimit)
        {
            _subTaskBuilder = subTaskBuilder;
        }

        #endregion


        #region ISyncEntityBundleTask Implementation

        /// <summary>
        /// Entity bundleId for sync
        /// </summary>
        public Guid EntityBundleId
        {
            get
            {
                return TaskData.EntityBundle.Oid;
            }
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Prepare sub tasks
        /// </summary>
        protected override void PrepareSubTasks()
        {
            // Capture changes from database
            CaptureChanges();
            // Get unsynced syncInstance bundles
            var unsyncedInstanceBundles = TaskData.GetUnsyncedInstanceBundles().ToArray();
            // Arrange high priority tasks
            ResolveClashWinners(unsyncedInstanceBundles);
            // Create sub task basic un synced instance bundles
            CreateSubTasks(unsyncedInstanceBundles);
        }

        /// <summary>
        /// On all sub task finished
        /// </summary>
        protected override void OnAllSubTasksFinished()
        {
        }

        /// <summary>
        /// Dispose to handle dispose object
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (TaskData != null)
            {
                TaskData.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// Check task disposed or not
        /// </summary>
        protected override void RunDisposedCheck()
        {
            base.RunDisposedCheck();

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Capture changes by change tracker
        /// </summary>
        private void CaptureChanges()
        {
            var activeEntities = TaskData.ActiveEntities;

            Log.Info("Starting Change Capture");

            foreach (var activeEntity in activeEntities)
            {
                if (!activeEntity.ChangeTrackerPluginId.HasValue)
                {
                    continue;
                }

                // Get change tracker plugin
                var changeTrackerPluginId = activeEntity.ChangeTrackerPluginId.Value;
                var changeTrackerPlugin = AutofacLocator.ResolveChangeTrackerPlugin<IChangeTrackerPlugin>(changeTrackerPluginId);

                if (changeTrackerPlugin == null)
                {
                    Log.Error("Change Tracker Plugin was not found by id = " + changeTrackerPluginId);
                    continue;
                }

                // Get agent plugin
                var agentPluginId = TaskData.GetAgentPluginId(activeEntity);
                var agentPlugin = AutofacLocator.ResolveAgentPlugin<IAgentPlugin>(agentPluginId);

                if (agentPlugin == null)
                {
                    Log.Error("Agent Plugin was not found by id = " + agentPluginId);
                    continue;
                }

                var changeTrackerConfiguration = agentPlugin.ChangeTrackerConfiguration;

                if (changeTrackerConfiguration == null)
                {
                    Log.Error("Change Tracker Plugin configuration not be found");
                    continue;
                }

                // Build change tracker
                using (var changeTracker = changeTrackerPlugin.BuildChangeTracker(changeTrackerConfiguration,
                    activeEntity.TableName,
                    activeEntity.GetDatabasePrimaryKey().Name,
                    activeEntity.LastChangeTrackerVersionId ?? 0))
                {
                    using (var agent = agentPlugin.BuildAgent())
                    {
                        IChange change;
                        // Any entity change
                        while ((change = changeTracker.GetNextChange()) != null)
                        {
                            try
                            {
                                ProcessChange(change, activeEntity, agent);
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Change capture failed", ex);
                                break;
                            }

                            Log.Info(string.Format("{0}: {1} Changed, has been add to sync task.", activeEntity.EntityName, change.Id));

                            var versionId = changeTracker.CommitChangeCaptured(change);
                            TaskData.UpdateTrackerVersionId(activeEntity, versionId);
                        }
                    }
                }
            }

            Log.Info("End Change Capture");
        }

        /// <summary>
        /// process change
        /// </summary>
        /// <param name="change">Change entity</param>
        /// <param name="syncEntity">Sync entity</param>
        /// <param name="agent">Agent of sync entity</param>
        private void ProcessChange(IChange change, SyncEntity syncEntity, IAgent agent)
        {
            if (change.Type == ChangeType.Create
                || change.Type == ChangeType.Update)
            {
                // Get sync instance
                var instance = TaskData.GetSyncInstance(syncEntity, change.Id);

                if (instance == null)
                {
                    // Create a sync instance for change sync entity
                    instance = TaskData.CreateSyncInstance(syncEntity, change.Id, null);
                    TrySetHash(agent, instance);
                }
                else
                {
                    // Mark current sync to change.
                    TaskData.MarkAsChanged(instance);
                    TryResolveFakeUpdate(agent, instance);
                }
            }
        }

        /// <summary>
        /// TrySetHash for sync instance
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="instance"></param>
        private void TrySetHash(IAgent agent, SyncInstance instance)
        {
            var hash = TryResolveEntityHash(agent, instance);

            if (hash != null)
            {
                TaskData.UpdateSyncInstanceHash(instance, hash);
            }
        }

        /// <summary>
        /// TryResolveFakeUpdate for check hash value for sync instance
        /// </summary>
        /// <param name="agent">Sync agent of sync instance</param>
        /// <param name="instance">Sync instance for sync</param>
        private void TryResolveFakeUpdate(IAgent agent, SyncInstance instance)
        {
            // Get hash of sync entity by sync instance
            var hash = TryResolveEntityHash(agent, instance);

            if (hash != null &&
                !Context.HashMaster.HashHexesAreEqual(instance.Hash, hash))
            {
                TaskData.UpdateSyncInstanceHash(instance, hash);
            }
        }

        /// <summary>
        /// TryResolveEntityHash for get sync entity hash
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="instance"></param>
        /// <returns></returns>
        private string TryResolveEntityHash(IAgent agent, SyncInstance instance)
        {
            IEntity entity;

            try
            {
                var primaryKeyInfo = instance.Entity.GetODataKey();
                if (primaryKeyInfo == null)
                {
                    throw new Exception("Please check your configuration and try it again.");
                }
                var filter =
                    new BinaryOperator(
                        new QueryOperand(primaryKeyInfo.Name, EntityAlias.DefaultEntityAlias,
                            DBColumn.GetColumnType(Type.GetType(primaryKeyInfo.Type))),
                        new ParameterValue { Value = instance.EntityRawId }, BinaryOperatorType.Equal);
                entity = agent.ReadOne(instance.Entity.EntityName, new List<EntityFieldInfo>(), filter);
            }
            catch (Exception ex)
            {
                Log.Error("Entity read failed", ex);
                entity = null;
            }

            return entity == null ? null : entity.GetEntityHash(Context.HashMaster);
        }

        /// <summary>
        /// Arrange high priority task to run first.
        /// </summary>
        /// <param name="unsyncedInstanceBundles">Sync instance bundle to be sync</param>
        private void ResolveClashWinners(IEnumerable<SyncInstanceBundle> unsyncedInstanceBundles)
        {
            foreach (var i in unsyncedInstanceBundles)
            {
                TaskData.LeaveOneClashWinner(i);
            }
        }

        /// <summary>
        /// Create sub tasks by unsync instance bundles
        /// </summary>
        /// <param name="unsyncedInstanceBundles"></param>
        private void CreateSubTasks(IEnumerable<SyncInstanceBundle> unsyncedInstanceBundles)
        {
            var syncInstanceBundleTasks = unsyncedInstanceBundles.Select(i => _subTaskBuilder.BuildTask(i, TaskData));

            foreach (var task in syncInstanceBundleTasks)
            {
                SubTasks.Add(task);
            }
        }

        #endregion
    }
}
