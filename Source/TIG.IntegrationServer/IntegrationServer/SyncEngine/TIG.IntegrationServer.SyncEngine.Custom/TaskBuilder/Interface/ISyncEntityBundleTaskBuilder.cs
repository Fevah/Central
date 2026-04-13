using System;
using TIG.IntegrationServer.SyncEngine.Custom.Task.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.TaskBuilder.Interface
{
    public interface ISyncEntityBundleTaskBuilder : ITaskBuilder
    {
        /// <summary>
        /// Build sync task by sync entity bundle
        /// </summary>
        /// <param name="entityBundleId">Sync entity bundle id</param>
        /// <returns>Sync entity bundle task</returns>
        ISyncEntityBundleTask BuildTask(Guid entityBundleId);
    }
}
