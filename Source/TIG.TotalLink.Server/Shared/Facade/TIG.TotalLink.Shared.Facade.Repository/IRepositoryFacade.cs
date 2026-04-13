using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Shared.DataModel.Repository;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Shared.Facade.Repository
{
    public interface IRepositoryFacade : IFacadeBase
    {
        /// <summary>
        /// File download initialze to download data.
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>Lenght of file</returns>
        Task<int> FileDownloadInitializeAsync(Guid fileId, Guid fileDataId);

        /// <summary>
        /// File Chunk download to client side.
        /// </summary>
        /// <param name="fileDataId">Id of file data</param>
        /// <param name="offset">Offset to take chunk</param>
        /// <returns>file chunk</returns>
        Task<byte[]> FileChunkDownloadAsync(Guid fileDataId, int offset);

        /// <summary>
        /// File upload done 
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>True, success to upload file.</returns>
        Task FileUploadDoneAsync(Guid fileId, Guid fileDataId);

        /// <summary>
        /// Upload chunk of upload file.
        /// </summary>
        /// <param name="fileId">Identity Id of upload file</param>
        /// <param name="offset">File offset to indicate chunk part of upload file</param>
        /// <param name="buffer">Chunk part of upload file</param>
        Task FileChunkUploadAsync(Guid fileId, int offset, byte[] buffer);

        /// <summary>
        /// Asynchronously updates the database schema on all repository data stores.
        /// </summary>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        Task UpdateAllDataStoresAsync(bool performUpdate);

        /// <summary>
        /// Asynchronously purges deleted items from all repository data stores.
        /// </summary>
        Task<List<PurgeResult>> PurgeAllDataStoresAsync();

        /// <summary>
        /// Asynchronously populates all repository data stores.
        /// </summary>
        Task PopulateAllDataStoresAsync();

        /// <summary>
        /// Creates a new repository store database file.
        /// </summary>
        /// <param name="dataStore">Information about the repository to create.</param>
        /// <returns>True if the database was created successfully, otherwise false.</returns>
        Task CreateDatabaseFile(DataStore dataStore);

        /// <summary>
        /// CreateRepositoryStore method for create repository store.
        /// </summary>
        /// <param name="dataStore">RepositoryInfo use for create repository store.</param>
        Task CreateRepositoryDataStore(DataStore dataStore);

        /// <summary>
        /// Delete repository store
        /// </summary>
        /// <param name="dataStore">Database information</param>
        Task DeleteRepositoryDataStore(DataStore dataStore);
    }
}