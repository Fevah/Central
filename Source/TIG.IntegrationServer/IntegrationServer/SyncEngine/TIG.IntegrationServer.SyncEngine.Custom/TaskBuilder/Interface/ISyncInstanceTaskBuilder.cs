using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Data;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;
using TIG.TotalLink.Shared.DataModel.Integration;

namespace TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface
{
    public interface ISyncInstanceTaskBuilder : ITaskBuilder
    {
        /// <summary>
        /// Build sync instance task.
        /// </summary>
        /// <param name="syncInstance">Sync instance for sync</param>
        /// <param name="map">Sync entity map for sync entity</param>
        /// <param name="taskeData">Sync task data</param>
        /// <returns>Sync instance task</returns>
        ISyncInstanceTask BuildTask(SyncInstance syncInstance, SyncEntityMap map, ITaskData taskeData);
    }
}
