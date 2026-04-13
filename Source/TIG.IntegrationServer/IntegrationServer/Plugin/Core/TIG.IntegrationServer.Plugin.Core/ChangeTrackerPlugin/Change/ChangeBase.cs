using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Enum;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change
{
    public abstract class ChangeBase : IChange
    {
        #region Public Properties

        /// <summary>
        /// Type to indicate entity change type.
        /// </summary>
        public virtual ChangeType Type { get; set; }

        /// <summary>
        /// Identity key for current change entity.
        /// </summary>
        public virtual string Id { get; set; }

        #endregion
    }
}
