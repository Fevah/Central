using System;

namespace TIG.IntegrationServer.SyncEngine.Custom.Task.Interface
{
    public interface ISyncEntityBundleTask : ITask
    {
        /// <summary>
        /// Get entity bundle id
        /// </summary>
        Guid EntityBundleId { get; }
    }
}
