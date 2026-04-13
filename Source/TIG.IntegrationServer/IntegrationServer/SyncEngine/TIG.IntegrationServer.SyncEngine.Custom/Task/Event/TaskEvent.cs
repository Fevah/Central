using TIG.IntegrationServer.SyncEngine.Core.Event;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Event
{
    public abstract class TaskEvent<TEventArgs> : SyncEngineEvent<TEventArgs, ITask>
        where TEventArgs : TaskEvent<TEventArgs>.Args
    {
        public new abstract class Args : SyncEngineEvent<TEventArgs, ITask>.Args
        {
            #region Constructors

            /// <summary>
            /// Default Constructor
            /// </summary>
            /// <param name="publisher">Task to trigger this event</param>
            protected Args(ITask publisher)
                : base(publisher)
            {
            }

            #endregion
        }
    }
}
