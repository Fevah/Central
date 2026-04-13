using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.ChangeTracker
{
    public abstract class ChangeTrackerBase : IChangeTracker
    {
        #region Public Methods

        /// <summary>
        /// Get next change from persistence.
        /// </summary>
        /// <returns>Change entity</returns>
        public abstract IChange GetNextChange();

        /// <summary>
        /// Commit change captured, it will compare internal queue.
        /// </summary>
        /// <param name="change">Change entity.</param>
        /// <returns>Retrieved change version id</returns>
        public abstract long CommitChangeCaptured(IChange change);

        /// <summary>
        /// Dispose relative objects.
        /// </summary>
        public void Dispose()
        {

        }

        #endregion

    }
}
