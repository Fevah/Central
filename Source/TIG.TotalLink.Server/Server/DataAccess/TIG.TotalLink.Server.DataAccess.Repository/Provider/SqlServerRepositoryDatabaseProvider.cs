using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using TIG.TotalLink.Server.DataAccess.Repository.Const;
using TIG.TotalLink.Server.DataAccess.Repository.DataModel;
using TIG.TotalLink.Server.DataAccess.Repository.Interface;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Repository;

namespace TIG.TotalLink.Server.DataAccess.Repository.Provider
{
    public class SqlServerRepositoryDatabaseProvider : IRepositoryDatabaseProvider
    {
        #region Fields

        private static readonly string CreateBlobedDatabasSqlTemplate;
        private static readonly string CreateStreamDatabasSqlTemplate;
        private static readonly string GetSqlServerDataDirectoryQuery;
        private static readonly string CreateDbFileSqlTemplate;
        private static readonly string CreateFileTableTemplate;
        private static readonly string CreateDataFolderTemplate;
        private static readonly string DeleteDatabaseTemplate;
        private static readonly string GetDatabaseStatsQuery;
        private readonly string _masterConnection;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor for initialize a sql database provider.
        /// </summary>
        /// <param name="server">Server use to create a repository store.</param>
        /// <param name="integratedSecurity">Indicate login by integrated or not.</param>
        /// <param name="loginName">Sql server login name.</param>
        /// <param name="password">Sql server password.</param>
        public SqlServerRepositoryDatabaseProvider(string server, bool integratedSecurity = false, string loginName = null, string password = null)
        {
            var connectionBuilder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                IntegratedSecurity = integratedSecurity,
                InitialCatalog = "master",
                UserID = loginName,
                Password = password
            };
            _masterConnection = connectionBuilder.ConnectionString;
        }

        /// <summary>
        /// Constructor for get embedded resource
        /// </summary>
        static SqlServerRepositoryDatabaseProvider()
        {
            var assembly = Assembly.GetExecutingAssembly();
            const string embeddedResourcesNameSpace = "TIG.TotalLink.Server.DataAccess.Repository.Resources.SqlServer.";
            CreateBlobedDatabasSqlTemplate = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "CreateBlobedDatabase.sql");
            CreateDbFileSqlTemplate = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "CreateDatabaseFile.sql");
            GetSqlServerDataDirectoryQuery = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "GetSqlServerDefaultDataDirectory.sql");
            CreateDataFolderTemplate = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "CreateDataFolder.sql");
            CreateStreamDatabasSqlTemplate = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "CreateStreamedDatabase.sql");
            CreateFileTableTemplate = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "CreateFileTable.sql");
            DeleteDatabaseTemplate = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "DeleteDatabase.sql");
            GetDatabaseStatsQuery = GetEmbeddedText(assembly, embeddedResourcesNameSpace + "GetDatabaseStats.sql");
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// CreateBlobedDatabase method use for create database which use to store small file.
        /// </summary>
        /// <param name="databaseName">Database name.</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <param name="location">Data file store location.</param>
        public void CreateBlobbedDatabase(string databaseName, double dataFileSizeLimit, string location = null)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                location = GetDefaultDatabaseDirectory();
            }

            using (var connection = new SqlConnection(_masterConnection))
            {
                connection.Open();
                // Top folder name is database name.
                CreateDataFolder(connection, location, databaseName);

                // Create first container
                var rootPath = string.Format(@"{0}\{1}", location, databaseName);
                CreateDataFolder(connection, rootPath, "Container_00001");

                // Create first folder
                var containerPath = string.Format(@"{0}\Container_00001", rootPath);
                CreateDataFolder(connection, containerPath, "Folder_00001");

                CreateDatabase(connection, CreateBlobedDatabasSqlTemplate, databaseName, dataFileSizeLimit, location);
                CreateFileTable(connection, databaseName, false);
                connection.Close();
            }
        }

        /// <summary>
        /// CreateStreamDatabase method use for create database which use to store big file.
        /// </summary>
        /// <param name="databaseName">Database name</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <param name="location">Data file store location.</param>
        public void CreateStreamDatabase(string databaseName, double dataFileSizeLimit, string location = null)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                location = GetDefaultDatabaseDirectory();
            }

            using (var connection = new SqlConnection(_masterConnection))
            {
                connection.Open();
                // Top folder name is database name.
                CreateDataFolder(connection, location, databaseName);

                // Create first container
                var rootPath = string.Format(@"{0}\{1}", location, databaseName);
                CreateDataFolder(connection, rootPath, "Container_00001");

                // Create first folder
                var containerPath = string.Format(@"{0}\Container_00001", rootPath);
                CreateDataFolder(connection, containerPath, "Folder_00001");

                CreateDatabase(connection, CreateStreamDatabasSqlTemplate, databaseName, dataFileSizeLimit, location);
                CreateFileTable(connection, databaseName, true);
                connection.Close();
            }
        }


        /// <summary>
        /// CreateDatabaseFile for create other database file when exist database file full.
        /// </summary>
        /// <param name="databaseName">Dabatase Name</param>
        /// <param name="dataFileSizeLimit">Size limit for data file</param>
        /// <param name="databaseFileInfo">According to databaseStorageStats to decide create new folder or not.</param>
        /// <returns>Database storage stats</returns>
        public DBStorageStats CreateDatabaseFile(string databaseName, double dataFileSizeLimit, DBStorageStats databaseFileInfo)
        {
            if (string.IsNullOrWhiteSpace(databaseFileInfo.Location))
            {
                databaseFileInfo.Location = GetDefaultDatabaseDirectory();
            }

            if (databaseFileInfo.FileCount >= 200)
            {
                if (databaseFileInfo.FolderCount >= 200)
                {
                    databaseFileInfo.ContainerCount++;
                    databaseFileInfo.FolderCount = 1;
                }
                else
                {
                    databaseFileInfo.FolderCount++;
                }

                databaseFileInfo.FileCount = 1;
            }
            else
            {
                databaseFileInfo.FileCount++;
            }

            var container = string.Format("Container_{0}", databaseFileInfo.ContainerCount.ToString("D5"));
            var folder = string.Format("Folder_{0}", databaseFileInfo.FolderCount.ToString("D5"));
            var rootPath = string.Format(@"{0}\{1}", databaseFileInfo.Location, databaseName);

            using (var connection = new SqlConnection(_masterConnection))
            {
                connection.Open();
                // Create first container
                CreateDataFolder(connection, rootPath, container);

                var containerPath = string.Format(@"{0}\{1}", rootPath, container);

                // Create first folder
                CreateDataFolder(connection, containerPath, container);

                var sql = CreateDbFileSqlTemplate
                    .Replace(SqlAliases.DbName, databaseName)
                    .Replace(SqlAliases.DbLocation, databaseFileInfo.Location)
                    .Replace(SqlAliases.Container, container)
                    .Replace(SqlAliases.Folder, folder)
                    .Replace(SqlAliases.DataFileSizeLimit, dataFileSizeLimit + "GB")
                    .Replace(SqlAliases.LogicalName, string.Format("{0}_PRIMARY_{1}", databaseName, databaseFileInfo.FileCount.ToString("D5")));
                ExecuteNonQuery(connection, sql);

                connection.Close();
            }

            return databaseFileInfo;
        }

        /// <summary>
        /// GetDatabaseStats for get database stats.
        /// </summary>
        /// <param name="databaseName">Dabatase Name</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <returns>Database storage stats</returns>
        public DBStorageStats GetDatabaseStats(string databaseName, double dataFileSizeLimit)
        {
            var dataFiles = new Dictionary<string, int>();
            string dbLogPath = null;

            using (var connection = new SqlConnection(_masterConnection))
            {
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = GetDatabaseStatsQuery;
                    cmd.Parameters.AddWithValue("@databaseName", databaseName);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fileTypeId = (byte)reader[2];
                            switch (fileTypeId)
                            {
                                case 0:
                                    var filePath = reader[0].ToString();
                                    var fileSize = (int)reader[1];
                                    dataFiles.Add(filePath, fileSize);
                                    break;
                                case 1:
                                    dbLogPath = reader[0].ToString();
                                    break;
                                default:
                                    throw new InvalidOperationException("Don't know how to process db file with typeId= " + fileTypeId);
                            }
                        }
                    }
                }

                connection.Close();
            }

            if (dataFiles.Count == 0)
            {
                return new DBStorageStats
                {
                    Status = DBStorageStatus.IsAbsent
                };
            }

            var pathSeparator = Path.DirectorySeparatorChar;
            var folders = new HashSet<string>();
            var containers = new HashSet<string>();

            foreach (var filePath in dataFiles.Keys)
            {
                var splittedPath = filePath.Split(pathSeparator);
                var fileNamePosition = splittedPath.Length - 1;
                var folder = splittedPath[fileNamePosition - 1];
                var container = splittedPath[fileNamePosition - 2];
                folders.Add(container + pathSeparator + folder);
                containers.Add(container);
            }

            var stats = new DBStorageStats
            {
                FileCount = dataFiles.Count,
                TotalSize = dataFiles.Values.Sum() * 8 / 1024,
                ContainerCount = containers.Count,
                FolderCount = folders.Count,
                FileUsedPercentage = dataFiles.Values.Sum() * 8 / (dataFileSizeLimit * 1024 * 1024 * dataFiles.Count),
                Location = Path.GetDirectoryName(dbLogPath),
                Status = DBStorageStatus.InSight
            };

            return stats;
        }

        /// <summary>
        /// Delete database
        /// </summary>
        /// <param name="databaseName">Database Name</param>
        public void DeleteDatabase(string databaseName)
        {
            var sql = DeleteDatabaseTemplate.Replace(SqlAliases.DbName, databaseName);
            using (var connection = new SqlConnection(_masterConnection))
            {
                connection.Open();
                ExecuteNonQuery(connection, sql);
                connection.Close();
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Create folder by sql server.
        /// </summary>
        /// <param name="connection">Sql connection</param>
        /// <param name="location">Data file store location.</param>
        /// <param name="folder">Folder name use to create a new folder.</param>
        private void CreateDataFolder(SqlConnection connection, string location, string folder)
        {
            var sql = CreateDataFolderTemplate
                .Replace(SqlAliases.DbLocation, location)
                .Replace(SqlAliases.Folder, folder);
            ExecuteNonQuery(connection, sql);
        }

        /// <summary>
        /// CreateFileTable for create 
        /// </summary>
        /// <param name="connection">Sql connection</param>
        /// <param name="databaseName">Database name</param>
        /// <param name="isFileStream">Indicate use file stream or not.</param>
        private void CreateFileTable(SqlConnection connection, string databaseName, bool isFileStream)
        {
            var sql = CreateFileTableTemplate.Replace(SqlAliases.DbName, databaseName)
                .Replace(SqlAliases.FileStream, isFileStream ? "FILESTREAM" : string.Empty);
            ExecuteNonQuery(connection, sql);
        }

        /// <summary>
        /// ExecuteNonQuery for perform query in sql server database.
        /// </summary>
        /// <param name="connection">Sql connection</param>
        /// <param name="sql">Sql query</param>
        private static void ExecuteNonQuery(SqlConnection connection, string sql)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// GetDefaultDatabaseDirectory for get default database directory.
        /// </summary>
        /// <returns>Directory path</returns>
        private string GetDefaultDatabaseDirectory()
        {
            using (var connection = new SqlConnection(_masterConnection))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = GetSqlServerDataDirectoryQuery;
                    command.CommandType = CommandType.Text;
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read()) return string.Empty;
                        var dbDirectory = reader.GetString(0);
                        connection.Close();
                        return dbDirectory;
                    }
                }
            }
        }

        /// <summary>
        /// CreateDatabase method for create database according to script.
        /// </summary>
        /// <param name="connection">Sql connection</param>
        /// <param name="dbSqlTemplate">Template use for create a database.</param>
        /// <param name="databaseName">Dabatase Name</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <param name="location">Data file store location.</param>
        private void CreateDatabase(SqlConnection connection, string dbSqlTemplate, string databaseName, double dataFileSizeLimit, string location)
        {
            var sql = dbSqlTemplate
                .Replace(SqlAliases.DbName, databaseName)
                .Replace(SqlAliases.DbLocation, location)
                .Replace(SqlAliases.Container, "Container_00001") //Initialize first container
                .Replace(SqlAliases.Folder, "Folder_00001") //Initialize first folder
                .Replace(SqlAliases.DataFileSizeLimit, (int)(dataFileSizeLimit * 1024) + "MB");

            ExecuteNonQuery(connection, sql);
        }

        /// <summary>
        /// GetEmbeddedText for read embedd text.
        /// </summary>
        /// <param name="assembly">Which assembly use to get resource.</param>
        /// <param name="resourceName">Resource name, full name.</param>
        /// <returns>Resource text</returns>
        private static string GetEmbeddedText(Assembly assembly, string resourceName)
        {
            string result;

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return string.Empty;
                using (var reader = new StreamReader(stream))
                {
                    result = reader.ReadToEnd();
                }
            }

            return result;
        }

        #endregion
    }
}