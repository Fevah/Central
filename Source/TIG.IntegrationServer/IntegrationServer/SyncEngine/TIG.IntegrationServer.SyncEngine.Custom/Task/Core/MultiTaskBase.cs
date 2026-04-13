using System.Collections.Generic;
using System.Linq;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Core
{
    public abstract class MultiTaskBase<TSubTask> : TaskBase
        where TSubTask : ITask
    {
        #region Protected Fields

        protected readonly IList<TSubTask> SubTasks = new List<TSubTask>();

        #endregion


        #region Constructors

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="log">Log writter</param>
        /// <param name="context">Sync context</param>
        /// <param name="taskData">Sync task data</param>
        protected MultiTaskBase(
            ILog log,
            IContext context,
            ITaskData taskData)
            : base(log, context, taskData)
        {
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Execute sync task
        /// </summary>
        protected override void ExecuteTask()
        {
            PrepareSubTasks();
            ExecuteSubTasks();
            OnAllSubTasksFinished();
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Prepare sub tasks
        /// </summary>
        protected abstract void PrepareSubTasks();

        /// <summary>
        /// Execute sub tasks
        /// </summary>
        protected abstract void ExecuteSubTasks();

        /// <summary>
        /// On all sub task finished
        /// </summary>
        protected virtual void OnAllSubTasksFinished()
        {
            if (SubTasks.Any(i => i.State == TaskState.Failed))
            {
                State = TaskState.CompletedPartially;
            }
        }

        #endregion
    }
}
