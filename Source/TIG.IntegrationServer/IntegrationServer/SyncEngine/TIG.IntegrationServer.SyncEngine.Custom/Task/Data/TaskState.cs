namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Data
{
    public enum TaskState
    {
        Undefined = 0,
        ReadyToRun = 2,
        Runnning = 3,
        Completed = 4,
        Failed = 5,
        Canceled = 6,
        Aborted = 7,
        CompletedPartially = 9,
    }

    public static class TaskStateExtensions
    {
        /// <summary>
        /// Can be disposed safely
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public static bool IsFinal(this TaskState t)
        {
            return t == TaskState.Completed
                   || t == TaskState.Failed
                   || t == TaskState.Canceled
                   || t == TaskState.Aborted
                   || t == TaskState.CompletedPartially;
        }
    }
}
