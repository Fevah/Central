using System;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Event;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Interface
{
    public interface ITask : IDisposable
    {
        /// <summary>
        /// Task state
        /// </summary>
        TaskState State { get; }

        /// <summary>
        /// Event when task status changed
        /// </summary>
        event EventHandler<TaskStateChangedEvent.Args> OnStateChanged;

        /// <summary>
        /// Begin execution task
        /// </summary>
        void BeginExecution();

        /// <summary>
        /// Finish execution task
        /// </summary>
        void EndExecution();
    }

    public static class TaskInterfaceExtensions
    {
        /// <summary>
        /// Execute task
        /// </summary>
        /// <param name="task">Task to be sync</param>
        public static void Execute(this ITask task)
        {
            task.BeginExecution();
            task.EndExecution();
        }
    }
}
