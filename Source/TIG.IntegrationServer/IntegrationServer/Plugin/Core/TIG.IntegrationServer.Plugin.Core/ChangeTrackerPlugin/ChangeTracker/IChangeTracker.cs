using System;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.ChangeTracker
{
    public interface IChangeTracker : IDisposable
    {
        /// <summary>
        /// Get next change from persistence.
        /// </summary>
        /// <returns>Change entity</returns>
        IChange GetNextChange();

        /// <summary>
        /// Commit change captured, it will compare internal queue.
        /// </summary>
        /// <param name="change">Change entity.</param>
        /// <returns>Retrieved change version id</returns>
        long CommitChangeCaptured(IChange change);
    }
}
