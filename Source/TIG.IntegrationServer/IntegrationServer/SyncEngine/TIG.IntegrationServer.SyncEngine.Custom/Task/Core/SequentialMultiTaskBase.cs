using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Core
{
    public abstract class SequentialMultiTaskBase<TSubTask> : MultiTaskBase<TSubTask>
        where TSubTask : ITask
    {
        #region Constructors

        protected SequentialMultiTaskBase(
            ILog log, 
            IContext context, 
            ITaskData taskData) 
            : base(log, context, taskData)
        {
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
                i.Execute();
            }
        }

        #endregion
    }
}
