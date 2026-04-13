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
    public abstract class TaskBase : SyncEngineContextConsumerComponent, ITask
    {
        #region Protected Fields

        protected readonly ITaskData TaskData;

        #endregion


        #region Private Fields

        private readonly ManualResetEvent _finishedEvent = new ManualResetEvent(false);

        private TaskState _state;
        private bool _disposed;

        #endregion


        #region Event

        /// <summary>
        /// Event for task status change 
        /// </summary>
        public event EventHandler<TaskStateChangedEvent.Args> OnStateChanged;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructors
        /// </summary>
        /// <param name="log">Log writter</param>
        /// <param name="context">Sync context</param>
        /// <param name="taskData">Sync relative task data</param>
        protected TaskBase(ILog log, IContext context, ITaskData taskData)
            : base(log, context)
        {
            TaskData = taskData;

            if (!Context.CancellationDispatcher.RegisterCancellationSubmitter(this))
            {
                State = TaskState.Aborted;
                return;
            }

            Context.PauseDispatcher.RegisterPauseSubmitter(this);

            State = TaskState.ReadyToRun;
        }

        #endregion


        #region ITask Implementation

        /// <summary>
        /// State for indicate current task status
        /// </summary>
        public TaskState State
        {
            get
            {
                RunDisposedCheck();

                return _state;
            }
            protected set
            {
                RunDisposedCheck();

                if (value == _state)
                {
                    return;
                }

                if (value.IsFinal())
                {
                    _finishedEvent.Set();
                }

                var previousState = _state;
                _state = value;

                FireOnStateChangedEvent(previousState);
            }
        }

        /// <summary>
        /// Begin task execution
        /// </summary>
        public void BeginExecution()
        {
            RunDisposedCheck();

            switch (State)
            {
                case TaskState.ReadyToRun:
                    State = TaskState.Runnning;
                    break;
                case TaskState.Aborted:
                    FireOnStateChangedEvent(TaskState.Undefined);
                    break;
                default:
                    throw new InvalidOperationException("Tried to run task more than once.");
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Context.PauseDispatcher.WaitUntilPauseRevokedIfPauseRequested(this);

                    if (Context.CancellationDispatcher.CancellationRequested)
                    {
                        State = TaskState.Canceled;
                        Context.CancellationDispatcher.SubmitCancellation(this);
                        return;
                    }

                    ExecuteTask();

                    if (State == TaskState.Runnning)
                    {
                        State = TaskState.Completed;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(string.Format("Error during task execution.  {0}", ex.Message), ex);
                    State = TaskState.Failed;
                }
            });
        }


        /// <summary>
        /// End execution task.
        /// </summary>
        public void EndExecution()
        {
            _finishedEvent.WaitOne();
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// ExecuteTask handle execute task logic
        /// </summary>
        protected abstract void ExecuteTask();

        #endregion


        #region Private Methods

        /// <summary>
        /// FireOnStateChangedEvent handle event change event
        /// </summary>
        /// <param name="previousState">Previous task state</param>
        private void FireOnStateChangedEvent(TaskState previousState)
        {
            if (OnStateChanged == null)
            {
                return;
            }

            var eventArgs = new TaskStateChangedEvent.Args(this, previousState);
            OnStateChanged(this, eventArgs);
        }

        #endregion


        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            Context.CancellationDispatcher.UnregisterCancellationSubmitter(this);
            Context.PauseDispatcher.UnregisterPauseSubmitter(this);

            if (_finishedEvent != null)
            {
                _finishedEvent.Set();
                _finishedEvent.Dispose();
            }

            _disposed = true;
        }

        protected override void RunDisposedCheck()
        {
            base.RunDisposedCheck();

            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        #endregion
    }
}
