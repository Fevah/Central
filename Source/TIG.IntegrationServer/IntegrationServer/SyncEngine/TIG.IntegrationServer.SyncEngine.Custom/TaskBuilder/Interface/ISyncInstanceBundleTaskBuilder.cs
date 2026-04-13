using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface
{
    public interface ISyncInstanceBundleTaskBuilder : ITaskBuilder
    {
        /// <summary>
        /// Build sync instance bundle task
        /// </summary>
        /// <param name="instanceBundle">Sync instance bundle</param>
        /// <param name="taskData">task data</param>
        /// <returns>Sync instance bundle task</returns>
        ISyncInstanceBundleTask BuildTask(SyncInstanceBundle instanceBundle, ITaskData taskData);
    }
}
