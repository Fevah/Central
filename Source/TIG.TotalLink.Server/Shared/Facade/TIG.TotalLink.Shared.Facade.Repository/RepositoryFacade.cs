using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Shared.Contract.Repository;
using TIG.TotalLink.Shared.DataModel.Repository;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Core.Extension;

namespace TIG.TotalLink.Shared.Facade.Repository
{
    [Facade(2, "Repository", 2, "Repository", true)]
    public class RepositoryFacade : FacadeBase<DataStore, IRepositoryMethodService>, IRepositoryFacade
    {
        #region Constructors

        public RepositoryFacade(IServiceConfiguration serviceConfiguration, ILocalStoreConfiguration localStoreConfiguration)
            : base(serviceConfiguration, localStoreConfiguration)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// File download initialze to download data.
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>Lenght of file</returns>
        public async Task<int> FileDownloadInitializeAsync(Guid fileId, Guid fileDataId)
        {
            return await MethodFacade.ExecuteAsync(c => c.FileDownloadInitialize(fileId, fileDataId)).ConfigureAwait(false);
        }

        /// <summary>
        /// File Chunk download to client side.
        /// </summary>
        /// <param name="fileDataId">Id of file data</param>
        /// <param name="offset">Offset to take chunk</param>
        /// <returns>file chunk</returns>
        public async Task<byte[]> FileChunkDownloadAsync(Guid fileDataId, int offset)
        {
            return await MethodFacade.ExecuteAsync(c => c.FileChunkDownload(fileDataId, offset)).ConfigureAwait(false);
        }

        /// <summary>
        /// File upload done 
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>True, success to upload file.</returns>
        public async Task FileUploadDoneAsync(Guid fileId, Guid fileDataId)
        {
            await MethodFacade.ExecuteAsync(c => c.FileUploadDone(fileId, fileDataId));
        }

        /// <summary>
        /// Upload chunk of upload file.
        /// </summary>
        /// <param name="fileId">Identity Id of upload file</param>
        /// <param name="offset">File offset to indicate chunk part of upload file</param>
        /// <param name="buffer">Chunk part of upload file</param>
        public async Task FileChunkUploadAsync(Guid fileId, int offset, byte[] buffer)
        {
            await MethodFacade.ExecuteAsync(c => c.FileChunkUpload(fileId, offset, buffer));
        }

        /// <summary>
        /// Asynchronously updates the database schema on all repository data stores.
        /// </summary>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        public async Task UpdateAllDataStoresAsync(bool performUpdate)
        {
            await MethodFacade.ExecuteAsync(c => c.UpdateAllDataStores(performUpdate)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously purges deleted items from all repository data stores.
        /// </summary>
        public async Task<List<PurgeResult>> PurgeAllDataStoresAsync()
        {
            return await MethodFacade.ExecuteAsync(c => c.PurgeAllDataStores()).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously populates all repository data stores.
        /// </summary>
        public async Task PopulateAllDataStoresAsync()
        {
            await MethodFacade.ExecuteAsync(c => c.PopulateAllDataStores()).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new repository store database file.
        /// </summary>
        /// <param name="dataStore">Information about the repository to create.</param>
        public async Task CreateDatabaseFile(DataStore dataStore)
        {
            var dataStoreJson = dataStore.SerializeToJson();
            await MethodFacade.ExecuteAsync(m => m.CreateDatabaseFile(dataStoreJson));
        }

        /// <summary>
        /// CreateRepositoryStore method for create repository store.
        /// </summary>
        /// <param name="dataStore">RepositoryInfo use for create repository store.</param>
        public async Task CreateRepositoryDataStore(DataStore dataStore)
        {
            var dataStoreJson = dataStore.SerializeToJson();
            await MethodFacade.ExecuteAsync(m => m.CreateRepositoryDataStore(dataStoreJson));
        }

        /// <summary>
        /// Delete repository store
        /// </summary>
        /// <param name="dataStore">Database information</param>
        public async Task DeleteRepositoryDataStore(DataStore dataStore)
        {
            var dataStoreJson = dataStore.SerializeToJson();
            await MethodFacade.ExecuteAsync(m => m.DeleteRepositoryDataStore(dataStoreJson));
        }

        #endregion
    }
}
