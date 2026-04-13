using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change;

namespace TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.ChangeTracker
{
    public abstract class SqlServerChangeTrackerBase : ChangeTrackerBase
    {
        #region Private Properties

        private Queue<SqlServerChangeBase> _changesQueue;

        #endregion


        #region Public Methods

        /// <summary>
        /// Get next change from database.
        /// </summary>
        /// <returns>Change entity</returns>
        public override IChange GetNextChange()
        {
            if (_changesQueue == null)
            {
                var orderedChanges = GetChanges()
                    .OrderBy(i => i.ChangeVersion)
                    .ToArray();
                var changesQueue = new Queue<SqlServerChangeBase>();

                foreach (var oc in orderedChanges)
                {
                    changesQueue.Enqueue(oc);
                }

                _changesQueue = changesQueue;
            }

            if (_changesQueue.Count == 0)
            {
                return null;
            }

            var change = _changesQueue.Peek();
            return change;
        }

        /// <summary>
        /// Commit change captured, it will compare internal queue.
        /// </summary>
        /// <param name="change">Change entity.</param>
        /// <returns>Retrieved change version id</returns>
        public override long CommitChangeCaptured(IChange change)
        {
            if (_changesQueue.Peek() != change)
            {
                throw new InvalidOperationException("Disorder!!!");
            }

            var capturedChange = _changesQueue.Dequeue();
            return capturedChange.ChangeVersion;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Get changes from database.
        /// </summary>
        /// <returns>Change entities</returns>
        private IEnumerable<SqlServerChangeBase> GetChanges()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = string.Format(
                        @"SELECT
                        SYS_CHANGE_VERSION,
                        SYS_CHANGE_OPERATION,
                        {1} AS CHANGETABLE_ID
                        FROM CHANGETABLE(CHANGES [{0}], @lastChangeVer) AS ct",
                        TableName,
                        PrimaryKeyColumnName);
                    cmd.Parameters.Add(new SqlParameter("@lastChangeVer", LastChangeVersionCaptured));

                    connection.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var change = ConstructChange(reader);
                            yield return change;
                        }
                    }
                }
            }
        }

        #endregion


        #region Abstract Methods
        
        /// <summary>
        /// Cunstruct change entity by persistence entity
        /// </summary>
        /// <param name="record">Persistence entity</param>
        /// <returns>Change entity</returns>
        protected abstract SqlServerChangeBase ConstructChange(IDataRecord record);

        #endregion


        #region Abstract Properties

        /// <summary>
        /// Sql connection string
        /// </summary>
        protected abstract string ConnectionString { get; }

        /// <summary>
        /// Table name of track entity.
        /// </summary>
        protected abstract string TableName { get; }

        /// <summary>
        /// Primary key column name.
        /// </summary>
        protected abstract string PrimaryKeyColumnName { get; }

        /// <summary>
        /// Last change version captured.
        /// </summary>
        protected abstract long LastChangeVersionCaptured { get; }

        #endregion
    }
}
