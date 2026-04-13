using System.Data;
using TIG.IntegrationServer.Common.Configuration;
using TIG.IntegrationServer.Plugin.ChangeTracker.SqlServerChangeTracker.Change;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.ChangeTracker;

namespace TIG.IntegrationServer.Plugin.ChangeTracker.SqlServerChangeTracker.ChangeTracker
{
    public class SqlServerChangeTracker : SqlServerChangeTrackerBase
    {
        #region Private Properties

        private readonly string _tableName;
        private readonly string _primaryKeyName;
        private readonly long _lastChangeTrackerVersionId;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        /// <param name="configuration">Change tracker configuration</param>
        /// <param name="tableName">Track table name</param>
        /// <param name="primaryKeyName">Primary key of track table</param>
        /// <param name="lastChangeTrackerVersionId">Last change tracker version id</param>
        public SqlServerChangeTracker(ChangeTrackerConfigurationElement configuration, string tableName,
            string primaryKeyName, long lastChangeTrackerVersionId)
        {
            _tableName = tableName;
            _primaryKeyName = primaryKeyName;
            _lastChangeTrackerVersionId = lastChangeTrackerVersionId;
            Configuration = configuration;
        }

        #endregion


        #region Protected Properties

        /// <summary>
        /// Change tracker configuration
        /// </summary>
        protected ChangeTrackerConfigurationElement Configuration { get; private set; }

        /// <summary>
        /// Primary key name of track table
        /// </summary>
        protected override string PrimaryKeyColumnName
        {
            get { return _primaryKeyName; }
        }

        /// <summary>
        /// Connection string of change track database
        /// </summary>
        protected override string ConnectionString
        {
            get { return Configuration.ConnectionString; }
        }

        /// <summary>
        /// Version of last change tracker
        /// </summary>
        protected override long LastChangeVersionCaptured
        {
            get { return _lastChangeTrackerVersionId; }
        }

        /// <summary>
        /// Table name of tracked
        /// </summary>
        protected override string TableName
        {
            get { return _tableName; }
        }

        #endregion


        #region Construct change

        /// <summary>
        /// ConstructChange for build a change by data record
        /// </summary>
        /// <param name="record">Data record</param>
        /// <returns></returns>
        protected override SqlServerChangeBase ConstructChange(IDataRecord record)
        {
            var change = new SqlServerChange(record);
            return change;
        }

        #endregion
    }
}