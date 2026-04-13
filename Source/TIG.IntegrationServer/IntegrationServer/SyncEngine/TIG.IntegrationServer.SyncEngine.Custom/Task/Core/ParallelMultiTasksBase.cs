using System;
using System.Threading;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Logging.Core.Extension;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Event;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Core
{
    public abstract class ParallelMultiTasksBase<TSubTask> : MultiTaskBase<TSubTask>
        where TSubTask : ITask
    {
        #region Private Fields

        private readonly Semaphore _concurrentSubTasksDelimiter;

        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="log">Log writter</param>
        /// <param name="context">Sync context</param>
        /// <param name="taskData">Sync task data</param>
        /// <param name="concurrentSubTasksLimit">Concurrent sub tasks limit</param>
        protected ParallelMultiTasksBase(
            ILog log,
            IContext context,
            ITaskData taskData,
            int concurrentSubTasksLimit)
            : base(log, context, taskData)
        {
            _concurrentSubTasksDelimiter = new Semaphore(concurrentSubTasksLimit, concurrentSubTasksLimit);
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Execute sub tasks
        /// </summary>
        protected override void ExecuteSubTasks()
        {
            foreach (var i in SubTasks)
            {
                i.OnStateChanged += OnSubTaskStateChanged;
            }

            BeginSubTasksExecution();
            EndSubTasksExecution();
        }

        /// <summary>
        /// Check entity was been diposed or not
        /// </summary>
        protected override void RunDisposedCheck()
        {
            base.RunDisposedCheck();

            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        /// <summary>
        /// Disposing to handle disposing event
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (_concurrentSubTasksDelimiter != null)
            {
                _concurrentSubTasksDelimiter.Dispose();
            }

            if (SubTasks != null)
            {
                foreach (var i in SubTasks)
                {
                    i.Dispose();
                }
            }

            _disposed = true;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Begin sub tasks exection
        /// </summary>
        private void BeginSubTasksExecution()
        {
            foreach (var task in SubTasks)
            {
                // Check system pause requested
                Context.PauseDispatcher.WaitUntilPauseRevokedIfPauseRequested(this);

                // Cancellation requested
                if (Context.CancellationDispatcher.CancellationRequested)
                {
                    State = TaskState.Canceled;
                    Context.CancellationDispatcher.SubmitCancellation(this);
                    break;
                }

                // Waitting for start run task
                _concurrentSubTasksDelimiter.WaitOne();

                try
                {
                    task.BeginExecution();
                }
                catch (Exception ex)
                {
                    Log.Error("Can not start sub task.", ex);
                    _concurrentSubTasksDelimiter.Release();
                }
            }
        }

        /// <summary>
        /// OnSubTaskStateChanged handle sub task state change event
        /// </summary>
        /// <param name="sender">Sub task</param>
        /// <param name="e">Event changed information</param>
        private void OnSubTaskStateChanged(object sender, TaskStateChangedEvent.Args e)
        {
            var syncTask = (ITask)sender;
            var newState = syncTask.State;

            if (newState.IsFinal())
            {
                syncTask.OnStateChanged -= OnSubTaskStateChanged;
                _concurrentSubTasksDelimiter.Release();
            }
        }

        /// <summary>
        /// EndSubTasksExecution handle end sub tasks execution event
        /// </summary>
        private void EndSubTasksExecution()
        {
            foreach (var i in SubTasks)
            {
                i.EndExecution();
            }
        }

        #endregion
    }
}
