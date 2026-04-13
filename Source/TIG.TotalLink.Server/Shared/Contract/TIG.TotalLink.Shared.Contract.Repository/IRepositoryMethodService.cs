using System;
using System.Collections.Generic;
using System.ServiceModel;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Shared.Contract.Core;

namespace TIG.TotalLink.Shared.Contract.Repository
{
    [ServiceContract]
    public interface IRepositoryMethodService : IMethodServiceBase
    {
        /// <summary>
        /// File download initialze to download data.
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>Lenght of file</returns>
        int FileDownloadInitialize(Guid fileId, Guid fileDataId);

        /// <summary>
        /// File Chunk download to client side.
        /// </summary>
        /// <param name="fileDataId">Id of file data</param>
        /// <param name="offset">Offset to take chunk</param>
        /// <returns>file chunk</returns>
        byte[] FileChunkDownload(Guid fileDataId, int offset);

        /// <summary>
        /// File upload done 
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>True, success to upload file.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void FileUploadDone(Guid fileId, Guid fileDataId);

        /// <summary>
        /// Upload chunk of upload file.
        /// </summary>
        /// <param name="fileDataId">Identity Id of upload file</param>
        /// <param name="offset">File offset to indicate chunk part of upload file</param>
        /// <param name="buffer">Chunk part of upload file</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void FileChunkUpload(Guid fileDataId, int offset, byte[] buffer);

        /// <summary>
        /// Updates the database schema on all repository data stores.
        /// </summary>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void UpdateAllDataStores(bool performUpdate);

        /// <summary>
        /// Purges deleted items from all repository data stores.
        /// </summary>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        List<PurgeResult> PurgeAllDataStores();

        /// <summary>
        /// Populates all repository data stores.
        /// </summary>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void PopulateAllDataStores();

        /// <summary>
        /// Creates a new repository store database file.
        /// </summary>
        /// <param name="dataStoreJson">Information about the data store to create.</param>
        /// <returns>True if the database was created successfully, otherwise false.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void CreateDatabaseFile(string dataStoreJson);

        /// <summary>
        /// CreateRepositoryStore method for create repository store.
        /// </summary>
        /// <param name="dataStoreJson">RepositoryInfo use for create repository store.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void CreateRepositoryDataStore(string dataStoreJson);

        /// <summary>
        /// Delete repository store
        /// </summary>
        /// <param name="dataStoreJson">Database information</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void DeleteRepositoryDataStore(string dataStoreJson);
    }
}