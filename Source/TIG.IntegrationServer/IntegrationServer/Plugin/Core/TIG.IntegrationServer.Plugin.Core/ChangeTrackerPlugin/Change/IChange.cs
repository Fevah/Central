using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Enum;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change
{
    public interface IChange
    {
        /// <summary>
        /// Type to indicate entity change type.
        /// </summary>
        ChangeType Type { get; }

        /// <summary>
        /// Identity key for current change entity.
        /// </summary>
        string Id { get; }
    }
}
