using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.ChangeTracker;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Interface;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin
{
    public abstract class ChangeTrackerPluginBase : IChangeTrackerPlugin
    {
        /// <summary>
        /// Build change tracker
        /// </summary>
        /// <param name="configuration">Change tracker configuration</param>
        /// <param name="tableName">Table name for track</param>
        /// <param name="primaryKeyName">Primary key of track table</param>
        /// <param name="lastChangeTrackerVersionId">Tracker version id of last change</param>
        /// <returns>Change tracker</returns>
        public abstract IChangeTracker BuildChangeTracker(ChangeTrackerConfigurationElement configuration, string tableName, string primaryKeyName, long lastChangeTrackerVersionId);
    }
}
