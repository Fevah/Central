using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.ChangeTracker;

namespace TIG.IntegrationServer.Plugin.ChangeTracker.SqlServerChangeTracker
{
    public class SqlServerChangeTrackerPlugin : ChangeTrackerPluginBase
    {
        #region Overrides

        /// <summary>
        /// Build sql server change tracker
        /// </summary>
        /// <param name="configuration">Change tracker configuration</param>
        /// <param name="tableName">Table name for track</param>
        /// <param name="primaryKeyName">Primary key of track table</param>
        /// <param name="lastChangeTrackerVersionId">Tracker version id of last change</param>
        /// <returns>Sql server change tracker</returns>
        public override IChangeTracker BuildChangeTracker(ChangeTrackerConfigurationElement configuration,
            string tableName, string primaryKeyName, long lastChangeTrackerVersionId)
        {
            var changeTracker = new ChangeTracker.SqlServerChangeTracker(configuration, tableName, primaryKeyName, lastChangeTrackerVersionId);
            return changeTracker;
        }

        #endregion

    }
}
