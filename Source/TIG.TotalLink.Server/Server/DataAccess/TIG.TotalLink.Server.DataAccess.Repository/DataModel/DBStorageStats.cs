using TIG.TotalLink.Shared.DataModel.Core.Enum.Repository;

namespace TIG.TotalLink.Server.DataAccess.Repository.DataModel
{
    public class DBStorageStats
    {
        #region Constructors
        
        /// <summary>
        /// Default Constructor
        /// </summary>
        public DBStorageStats() { }

        /// <summary>
        /// Constructor with initilize properties
        /// </summary>
        /// <param name="location">Indicate root path to database file</param>
        /// <param name="containerCount">Container folder count</param>
        /// <param name="folderCount">Folder count</param>
        /// <param name="fileCount">Data files count </param>
        public DBStorageStats(string location, int containerCount, int folderCount, int fileCount)
        {
            Location = location;
            ContainerCount = containerCount;
            FolderCount = folderCount;
            FileCount = fileCount;
        }

        #endregion


        #region Properties
        
        /// <summary>
        /// Count of container folder
        /// </summary>
        public int ContainerCount { get; set; }
        
        /// <summary>
        /// Count of folder
        /// </summary>
        public int FolderCount { get; set; }
        
        /// <summary>
        /// Count of file of database
        /// </summary>
        public int FileCount { get; set; }

        /// <summary>
        /// File used percentage of database
        /// </summary>
        public double FileUsedPercentage { get; set; }

        /// <summary>
        /// Total size of database
        /// </summary>
        public double TotalSize { get; set; }

        /// <summary>
        /// Location of databaise file
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Database status
        /// </summary>
        public DBStorageStatus Status { get; set; }
        
        #endregion
    }
}