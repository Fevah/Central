using TIG.TotalLink.Server.DataAccess.Repository.DataModel;

namespace TIG.TotalLink.Server.DataAccess.Repository.Interface
{
    public interface IRepositoryDatabaseProvider
    {
        #region Public Methods

        /// <summary>
        /// CreateBlobedDatabase method use for create database which use to store small file.
        /// </summary>
        /// <param name="databaseName">Database name.</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <param name="location">Data file store location.</param>
        void CreateBlobbedDatabase(string databaseName, double dataFileSizeLimit, string location = null);

        /// <summary>
        /// CreateStreamDatabase method use for create database which use to store big file.
        /// </summary>
        /// <param name="databaseName">Database name</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <param name="location">Data file store location.</param>
        void CreateStreamDatabase(string databaseName, double dataFileSizeLimit, string location = null);

        /// <summary>
        /// CreateDatabaseFile for create other database file when exist database file full.
        /// </summary>
        /// <param name="databaseName">Dabatase Name</param>
        /// <param name="dataFileSizeLimitGB">Data file size limit</param>
        /// <param name="databaseFileInfo">According to databaseStorageStats to decide create new folder or not.</param>
        /// <returns>Database storage stats</returns>
        DBStorageStats CreateDatabaseFile(string databaseName, double dataFileSizeLimitGB, DBStorageStats databaseFileInfo);

        /// <summary>
        /// GetDatabaseStats for get database stats.
        /// </summary>
        /// <param name="databaseName">Dabatase Name</param>
        /// <param name="dataFileSizeLimit">DataFileSizeLimit used limit database size when create database file.</param>
        /// <returns>Database storage stats</returns>
        DBStorageStats GetDatabaseStats(string databaseName, double dataFileSizeLimit);

        /// <summary>
        /// Delete database
        /// </summary>
        /// <param name="databaseName">Database Name</param>
        void DeleteDatabase(string databaseName);

        #endregion
    }
}