using TIG.IntegrationServer.SyncEngine.Custom.Task.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Event
{
    public class TaskStateChangedEvent : TaskEvent<TaskStateChangedEvent.Args>
    {
        public new class Args : TaskEvent<Args>.Args
        {
            #region Constructors

            /// <summary>
            /// Default Constructor
            /// </summary>
            /// <param name="publisher">Task</param>
            /// <param name="previousState">Previous task state</param>
            public Args(ITask publisher, TaskState previousState)
                : base(publisher)
            {
                PreviousState = previousState;
            }

            #endregion


            #region Public Properties

            /// <summary>
            /// The previous state of the task.
            /// </summary>
            public TaskState PreviousState { get; private set; }

            #endregion
        }
    }
}
