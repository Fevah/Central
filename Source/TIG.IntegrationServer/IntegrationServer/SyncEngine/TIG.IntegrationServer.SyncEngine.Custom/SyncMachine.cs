using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.SyncEngine.Core.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Event;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface;
using TIG.IntegrationServer.TimeoutManager.Core.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom
{
    public class SyncMachine : SyncEngineContextConsumerComponent, ISyncMachine
    {
        #region Private Fields

        private readonly ConcurrentBag<ISyncEntityBundleTask> _runningTasks = new ConcurrentBag<ISyncEntityBundleTask>();
        private readonly Semaphore _concurrentTasksDelimiter;
        private readonly ISyncEntityBundleTaskBuilder _taskBuilder;
        private readonly IActionScheduler _actionScheduler;

        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with components.
        /// </summary>
        /// <param name="log">Log writer.</param>
        /// <param name="context">Sync context.</param>
        /// <param name="taskBuilder">Sync entity task builder.</param>
        /// <param name="actionScheduler">Action scheduler.</param>
        public SyncMachine(ILog log, IContext context, ISyncEntityBundleTaskBuilder taskBuilder, IActionScheduler actionScheduler)
            : base(log, context)
        {
            // Create a Semaphore for managing concurrent tasks
            var concurrentSyncTypesLimit = context.Configuration.ConcurrentSyncTaskLimit;
            _concurrentTasksDelimiter = new Semaphore(concurrentSyncTypesLimit, concurrentSyncTypesLimit);

            // Store related objects
            _taskBuilder = taskBuilder;
            _actionScheduler = actionScheduler;
        }

        #endregion


        #region ISyncMachine Methods

        /// <summary>
        /// Start the sync machine.
        /// </summary>
        public void Start()
        {
            RunDisposedCheck();

            Log.Info("Starting...");

            System.Threading.Tasks.Task.Run(() => CreateAndRunTasksForAllActiveEntityBundles());

            Log.Info("Started");
        }

        /// <summary>
        /// Pause the sync machine.
        /// </summary>
        public void Pause()
        {
            RunDisposedCheck();

            Log.Info("Pausing...");

            Context.PauseDispatcher.RequestPauseAndWaitForSubmission();

            Log.Info("Paused");
        }

        /// <summary>
        /// Continue the sync machine when it is paused.
        /// </summary>
        public void Continue()
        {
            RunDisposedCheck();

            Log.Info("Unpausing...");

            Context.PauseDispatcher.RevokePauseRequest();

            Log.Info("Unpaused");
        }

        /// <summary>
        /// Stop the sync machine.
        /// </summary>
        public void Stop()
        {
            RunDisposedCheck();

            Log.Info("Stopping...");

            Context.PauseDispatcher.RevokePauseRequestIfPauseRequested();
            Context.CancellationDispatcher.RequestCancellationAndWaitForSubmission();

            Log.Info("Stopped");
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Create and run tasks for all active entity bundles.
        /// </summary>
        private void CreateAndRunTasksForAllActiveEntityBundles()
        {
            // Attempt to get Id's of all active bundles
            var activeSyncEntityBundlesIds = Context.Configuration.GetIdsOfActiveSyncEntityBundles().ToArray();

            if (activeSyncEntityBundlesIds.Length > 0)
            {
                // If active bundles were found, start a task for each bundle
                foreach (var id in activeSyncEntityBundlesIds)
                {
                    CreateAndRunTask(id);
                }
            }
            else
            {
                // If no active bundles were found, start a task to look for active bundles again after a delay
                var delay = Context.Configuration.ActiveSyncTasksPollingTimeout;
                _actionScheduler.InvokeActionWithDelay(CreateAndRunTasksForAllActiveEntityBundles, delay);
            }
        }

        /// <summary>
        /// Create and run task by sync entity bundles.
        /// </summary>
        /// <param name="syncEntityBundlesId">Sync entity bundles to sync.</param>
        private void CreateAndRunTask(Guid syncEntityBundlesId)
        {
            // If pause has been requested, do nothing until it is revoked
            Context.PauseDispatcher.WaitUntilPauseRevokedIfPauseRequested();

            // Abort if cancellation has been requested
            if (Context.CancellationDispatcher.CancellationRequested)
            {
                return;
            }

            // Wait for other tasks
            _concurrentTasksDelimiter.WaitOne();

            // Abort if cancellation has been requested
            if (Context.CancellationDispatcher.CancellationRequested)
            {
                _concurrentTasksDelimiter.Release();
                return;
            }

            // Create a new task and add it to runningTasks
            var task = _taskBuilder.BuildTask(syncEntityBundlesId);
            task.OnStateChanged += OnTaskStateChanged;
            _runningTasks.Add(task);

            // Start the new task
            task.BeginExecution();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the OnTaskStateChanged for all tasks.
        /// </summary>
        /// <param name="sender">Object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void OnTaskStateChanged(object sender, TaskStateChangedEvent.Args e)
        {
            // Get useful task values
            var syncTask = (ISyncEntityBundleTask)sender;
            var newState = syncTask.State;
            var syncTypeId = syncTask.EntityBundleId;

            // If the task is finished...
            if (newState.IsFinal())
            {
                // Dispose the task
                syncTask.OnStateChanged -= OnTaskStateChanged;
                _runningTasks.TryTake(out syncTask);
                syncTask.Dispose();

                // Restart the task after a delay
                var timeout = Context.Configuration.GetSyncTaskTimeout(syncTypeId);
                _actionScheduler.InvokeActionWithDelay(() => CreateAndRunTask(syncTypeId), timeout);

                // Flag that the task has completed
                _concurrentTasksDelimiter.Release();
            }
        }

        #endregion


        #region Overrides

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (_taskBuilder != null)
            {
                _taskBuilder.Dispose();
            }

            if (_runningTasks != null)
            {
                foreach (var i in _runningTasks)
                {
                    i.Dispose();
                }
            }

            _disposed = true;
        }

        protected override void RunDisposedCheck()
        {
            base.RunDisposedCheck();

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #endregion
    }
}
