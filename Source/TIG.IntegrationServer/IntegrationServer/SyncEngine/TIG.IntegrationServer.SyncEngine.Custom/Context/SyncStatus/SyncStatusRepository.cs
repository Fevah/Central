using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using TIG.IntegrationServer.SyncEngine.Core;
using TIG.IntegrationServer.SyncEngine.Core.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.SyncStatus
{
    public class SyncStatusRepository : ISyncStatusRepository, IDisposable
    {
        #region Private Properties

        private readonly SQLiteConnection _syncStatusDbConnection;

        #endregion


        #region Default Constructors

        public SyncStatusRepository()
        {
            var dbPath = string.Format("{0}\\SyncStatus.db", Environment.CurrentDirectory);
            var syncStatusDbConnectionStr = string.Format("Data Source={0}", dbPath);

            if (_syncStatusDbConnection == null)
            {
                _syncStatusDbConnection = new SQLiteConnection(syncStatusDbConnectionStr);
            }

            if (!File.Exists(dbPath))
            {
                CreateDatabase(_syncStatusDbConnection);
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Create sync status database.
        /// </summary>
        /// <param name="connection"></param>
        private static void CreateDatabase(IDbConnection connection)
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = (@"create table SyncEntityStatus
                                                  (
                                                     EntityKey                  varchar(100) not null,
                                                     SourceAgentId         varchar(100) not null,
                                                     TargetAgentId          varchar(100) not null,
                                                     SyncTime               datetime not null
                                                  )");
                command.ExecuteNonQuery();
            }

            connection.Close();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Get last sync time from persistence
        /// </summary>
        /// <param name="entityKey">Sync entity key</param>
        /// <param name="sourceAgentId">Source agent id</param>
        /// <param name="targetAgentId">Target agent id</param>
        /// <returns>Last sync entity time</returns>
        public DateTime? GetLastSyncTime(string entityKey, string sourceAgentId, string targetAgentId)
        {
            try
            {
                using (var command = new SQLiteCommand(_syncStatusDbConnection)
                {
                    CommandText =
                        "Select SyncTime from SyncEntityStatus where EntityKey = @entityKey and SourceAgentId = @sourceAgentId and TargetAgentId = @targetAgentId;" +
                        "Delete from SyncEntityStatus where EntityKey = @entityKey and SourceAgentId = @sourceAgentId and TargetAgentId = @targetAgentId;"
                })
                {
                    command.Parameters.AddRange(new[]
                    {
                        new SQLiteParameter(DbType.String)
                        {
                            ParameterName = "@entityKey",
                            Value = entityKey
                        },
                        new SQLiteParameter(DbType.String)
                        {
                            ParameterName = "@sourceAgentId",
                            Value = sourceAgentId
                        },
                        new SQLiteParameter(DbType.String)
                        {
                            ParameterName = "@targetAgentId",
                            Value = targetAgentId
                        }
                    });
                    _syncStatusDbConnection.Open();
                    var result = command.ExecuteScalar();
                    return result == DBNull.Value || result == null ? (DateTime?)null : (DateTime)result;
                }
            }
            finally
            {
                _syncStatusDbConnection.Close();
            }
        }

        /// <summary>
        /// Create sync entity status in persistence
        /// </summary>
        /// <param name="syncEntityStatus">SyncEntityStatus to record sync entity information with agent information.</param>
        /// <returns>True, it indicate persistence successfull.</returns>
        public void CreateSyncEntityStatus(ISyncEntityStatus syncEntityStatus)
        {
            try
            {
                using (var command = new SQLiteCommand(_syncStatusDbConnection)
                {
                    CommandText =
                        "Insert into SyncEntityStatus values(@entityKey, @sourceAgentId, @targetAgentId, @syncTime)"
                })
                {
                    command.Parameters.AddRange(new[]
                    {
                        new SQLiteParameter(DbType.String)
                        {
                            ParameterName = "@entityKey",
                            Value = syncEntityStatus.EntityKey
                        },
                        new SQLiteParameter(DbType.String)
                        {
                            ParameterName = "@sourceAgentId",
                            Value = syncEntityStatus.SourceAgentId
                        },
                        new SQLiteParameter(DbType.String)
                        {
                            ParameterName = "@targetAgentId",
                            Value = syncEntityStatus.TargetAgentId
                        },
                        new SQLiteParameter(DbType.DateTime)
                        {
                            ParameterName = "@syncTime",
                            Value = syncEntityStatus.SynceTime
                        }
                    });
                    _syncStatusDbConnection.Open();
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                _syncStatusDbConnection.Close();
            }
        }

        public void Dispose()
        {
            if (_syncStatusDbConnection != null)
            {
                _syncStatusDbConnection.Dispose();
            }
        }

        #endregion
    }
}