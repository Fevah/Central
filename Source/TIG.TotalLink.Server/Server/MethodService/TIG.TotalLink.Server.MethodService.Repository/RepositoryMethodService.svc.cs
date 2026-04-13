using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.ServiceModel;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Server.DataAccess.Repository;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.Contract.Repository;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Repository;
using TIG.TotalLink.Shared.DataModel.Core.Extension;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.DataModel.Repository;
using TIG.TotalLink.Shared.DataModel.RepositoryStore.DataModel;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Global;
using TIG.TotalLink.Shared.Facade.Repository;
using TIG.TotalLink.Shared.Xpo.Core.Helper;
using File = TIG.TotalLink.Shared.DataModel.Repository.File;

namespace TIG.TotalLink.Server.MethodService.Repository
{
    public class RepositoryMethodService : MethodServiceBase, IRepositoryMethodService
    {
        #region Private Fields

        private static IGlobalFacade _globalFacade;
        private static IRepositoryFacade _repositoryFacade;
        private static Assembly[] _repositoryStoreAssemblies;
        private static readonly Dictionary<string, FileStream> WcfUploadFileStreams = new Dictionary<string, FileStream>();
        private static readonly Dictionary<string, byte[]> WcfDownloadFileStreams = new Dictionary<string, byte[]>();
        private static readonly string EnvorinmentPath = System.Web.Hosting.HostingEnvironment.MapPath("~");
        private const int Buffersize = 1024 * 16;

        #endregion


        #region Constructors

        public RepositoryMethodService()
        {
            // Create an array containing datamodel assemblies required for a repository store database
            _repositoryStoreAssemblies = new[]
            {
                typeof(FileData).Assembly,
                typeof(XPObjectType).Assembly
            };
        }

        #endregion


        #region Service Methods

        /// <summary>
        /// File download initialze to download data.
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        /// <returns>Lenght of file</returns>
        public int FileDownloadInitialize(Guid fileId, Guid fileDataId)
        {
            var filePath = EnvorinmentPath + fileDataId.ToString("N");

            lock (WcfDownloadFileStreams)
            {
                if (WcfDownloadFileStreams.ContainsKey(filePath))
                {
                    return WcfDownloadFileStreams[filePath].Length;
                }
            }

            // Get relevant data store by file information.
            var dataStore = GetDataStoreByFile(fileId);

            if (dataStore == null)
            {
                throw new FaultException<ServiceFault>(
                    new ServiceFault("File cannot be found."));
            }

            // Read file from corresponding datastore.
            var file = ReadFileFromDataStore(fileDataId, dataStore);

            if (file == null)
            {
                throw new FaultException<ServiceFault>(
                    new ServiceFault("File data not be found in datastore."));
            }

            // Catch up file to cache
            lock (WcfDownloadFileStreams)
            {
                WcfDownloadFileStreams[filePath] = file;
            }

            // Retrive file Length.
            return file.Length;
        }

        /// <summary>
        /// File Chunk download to client side.
        /// </summary>
        /// <param name="fileDataId">Id of file data</param>
        /// <param name="offset">Offset to take chunk</param>
        /// <returns>file chunk</returns>
        public byte[] FileChunkDownload(Guid fileDataId, int offset)
        {
            var filepath = EnvorinmentPath + fileDataId.ToString("N");

            byte[] file;
            lock (WcfDownloadFileStreams)
            {
                if (!WcfDownloadFileStreams.TryGetValue(filepath, out file)
                    || file == null)
                {
                    throw new FaultException<ServiceFault>(
                                                new ServiceFault(string.Format("There are don't have any file({0}) in service cache.", fileDataId)));
                }
            }

            // Take chuck of upload file.
            var buffer = file.Skip(offset).Take(Buffersize).ToArray();

            return buffer;
        }

        /// <summary>
        /// File upload done 
        /// </summary>
        /// <param name="fileId">Id of file specificatioin</param>
        /// <param name="fileDataId">Id of file data</param>
        public void FileUploadDone(Guid fileId, Guid fileDataId)
        {
            var filepath = EnvorinmentPath + fileDataId.ToString("N");

            lock (WcfUploadFileStreams)
            {
                // Get file data from service cache.
                FileStream fs;
                if (!WcfUploadFileStreams.TryGetValue(filepath, out fs)
                    || fs == null)
                {
                    throw new FaultException<ServiceFault>(
                        new ServiceFault(string.Format("There are don't have any file({0}) in service cache.", fileDataId)));
                }

                WcfUploadFileStreams.Remove(filepath);

                // Read data from file stream.
                var fileData = new byte[fs.Length];

                try
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    fs.Read(fileData, 0, fileData.Length);
                }
                catch (Exception ex)
                {
                    throw new FaultException<ServiceFault>(
                        new ServiceFault("Save to data store failed, Please try it again.", ex));
                }
                finally
                {
                    fs.Close();
                }

                // Get relevant data store by file information.
                var dataStore = GetDataStoreByFile(fileId);

                if (dataStore == null)
                {
                    throw new FaultException<ServiceFault>(
                        new ServiceFault("Please make sure datastore settings is right."));
                }

                // Save file data to relevant data store.
                SaveFileToDataStore(fileDataId, dataStore, fileData);

                // Link file to a default file version.
                _repositoryFacade.ExecuteUnitOfWork(uow =>
                {
                    var file = uow.GetObjectByKey<File>(fileId);
                    var dataStoreInSession = uow.GetDataObject(dataStore);
                    new FileVersion(uow)
                    {
                        FileDataID = fileDataId,
                        Version = 0, // Set default version 0.
                        File = file,
                        DataStore = dataStoreInSession
                    };
                });
            }
        }

        /// <summary>
        /// Upload chunk of upload file.
        /// </summary>
        /// <param name="fileDataId">Identity Id of upload file</param>
        /// <param name="offset">File offset to indicate chunk part of upload file</param>
        /// <param name="buffer">Chunk part of upload file</param>
        public void FileChunkUpload(Guid fileDataId, int offset, byte[] buffer)
        {
            // Make file identity by file Id.
            var filepath = EnvorinmentPath + fileDataId.ToString("N");
            try
            {
                FileStream fs;
                lock (WcfUploadFileStreams)
                {
                    WcfUploadFileStreams.TryGetValue(filepath, out fs);
                    if (fs == null)
                    {
                        fs = System.IO.File.Open(filepath, FileMode.Create, FileAccess.ReadWrite);
                        WcfUploadFileStreams.Add(filepath, fs);
                    }
                }
                fs.Write(buffer, 0, buffer.Length);
                fs.Flush();
            }
            catch (Exception ex)
            {
                // Anything wrong, system will remove it from service cache.
                if (WcfUploadFileStreams != null)
                {
                    lock (WcfUploadFileStreams)
                    {
                        WcfUploadFileStreams.Remove(filepath);
                    }
                }

                throw new FaultException<ServiceFault>(new ServiceFault("Upload failed, Please try it again.", ex));
            }
        }

        /// <summary>
        /// Updates the database schema on all repository data stores.
        /// </summary>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        public void UpdateAllDataStores(bool performUpdate)
        {
            foreach (var connectionString in GetAllDataStoreConnectionStrings())
            {
                ServiceHelper.UpdateDatabase(performUpdate, connectionString, _repositoryStoreAssemblies, false);
            }
        }

        /// <summary>
        /// Purges deleted items from all repository data stores.
        /// </summary>
        public List<PurgeResult> PurgeAllDataStores()
        {
            var purgeResults = new List<PurgeResult>();

            foreach (var connectionString in GetAllDataStoreConnectionStrings())
            {
                purgeResults.Add(ServiceHelper.PurgeDatabase(connectionString, _repositoryStoreAssemblies));
            }

            return purgeResults;
        }

        /// <summary>
        /// Populates all repository data stores.
        /// </summary>
        public void PopulateAllDataStores()
        {
            foreach (var connectionString in GetAllDataStoreConnectionStrings())
            {
                ServiceHelper.PopulateDataStore(connectionString, _repositoryStoreAssemblies);
            }
        }

        /// <summary>
        /// Creates a new repository store database file.
        /// </summary>
        /// <param name="dataStoreJson">DataStore json string use for create data store file.</param>
        /// <returns>True if the database was created successfully, otherwise false.</returns>
        public void CreateDatabaseFile(string dataStoreJson)
        {
            var dataStore = JsonHelper.DeserializeDataObject<DataStore>(dataStoreJson);

            try
            {
                var repositoryDatabaseProvider = RepositoryDatabaseProviderFactory.GetRepositoryDatabaseProvider(dataStore);
                var databaseFileInfo = repositoryDatabaseProvider.GetDatabaseStats(dataStore.DatabaseName, dataStore.DataFileSizeLimit);
                repositoryDatabaseProvider.CreateDatabaseFile(dataStore.DatabaseName, dataStore.DataFileSizeLimit, databaseFileInfo);
            }
            catch (SqlException ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(
                        string.Format("Create database file of repository store '{0}' failed.",
                            dataStore.DatabaseName), ex));
            }
        }

        /// <summary>
        /// CreateRepositoryDataStore method for create data store.
        /// </summary>
        /// <param name="dataStoreJson">DataStore json string use for create data store.</param>
        public void CreateRepositoryDataStore(string dataStoreJson)
        {
            var dataStore = JsonHelper.DeserializeDataObject<DataStore>(dataStoreJson);

            var repositoryDatabaseProvider = RepositoryDatabaseProviderFactory.GetRepositoryDatabaseProvider(dataStore);

            if (repositoryDatabaseProvider == null)
            {
                return;
            }

            try
            {
                switch (dataStore.Type)
                {
                    case RepositoryType.BlobbedStore:
                        repositoryDatabaseProvider.CreateBlobbedDatabase(dataStore.DatabaseName, dataStore.DataFileSizeLimit);
                        break;

                    case RepositoryType.FileStreamStore:
                        repositoryDatabaseProvider.CreateStreamDatabase(dataStore.DatabaseName, dataStore.DataFileSizeLimit);
                        break;
                }
            }
            catch (SqlException ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(
                    string.Format("Create repository store '{0}' failed.",
                        dataStore.DatabaseName), ex));
            }
        }

        /// <summary>
        /// Delete repository data store
        /// </summary>
        /// <param name="dataStoreJson">DataStore json string use for create data store.</param>
        public void DeleteRepositoryDataStore(string dataStoreJson)
        {
            var dataStore = JsonHelper.DeserializeDataObject<DataStore>(dataStoreJson);

            var repositoryDatabaseProvider = RepositoryDatabaseProviderFactory.GetRepositoryDatabaseProvider(dataStore);

            if (repositoryDatabaseProvider == null)
            {
                return;
            }

            try
            {
                repositoryDatabaseProvider.DeleteDatabase(dataStore.DatabaseName);
            }
            catch (SqlException ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(
                    string.Format("Delete repository '{0}' failed.",
                        dataStore.DatabaseName), ex));
            }
        }

        #endregion


        #region Private Methods

        private byte[] ReadFileFromDataStore(Guid fileDataId, DataStore dataStore)
        {
            // Get global data facade.
            GetGlobalDataFacade();

            // Attempt to get the selected XpoProvider
            var xpoProvider =
                _globalFacade.ExecuteQuery(
                    uow => uow.Query<XpoProvider>().Where(p => p.Name == dataStore.DatabaseProvider.ToString()))
                    .FirstOrDefault();
            if (xpoProvider == null)
                throw new FaultException<ServiceFault>(
                    new ServiceFault(string.Format("Unknown provider on store named {0}!", dataStore.DatabaseName)));

            // Get the connection string and add it to the list
            var connectionString = ServiceHelper.GetConnectionString(xpoProvider.Name,
                xpoProvider.HasUserName,
                xpoProvider.HasPassword,
                xpoProvider.IsServerBased,
                dataStore.Server,
                dataStore.DatabaseName, null,
                dataStore.IntegratedSecurity,
                dataStore.UserName,
                dataStore.Password);

            // Attempt to connect to the database without upgrading, to determine if an upgrade is required
            IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;

            try
            {
                // Calling UpdateSchema when the data layer was created with AutoCreateOption.None will throw an error if the database needs an update
                var dataLayer = ServiceHelper.CreateDatabaseDataLayer(connectionString, AutoCreateOption.None,
                    _repositoryStoreAssemblies,
                    out objectsToDisposeOnDisconnect);

                // Save file to data store.
                if (dataLayer != null)
                {
                    using (var uow = new UnitOfWork(dataLayer))
                    {
                        var file = uow.Query<FileData>().FirstOrDefault(p => p.Oid == fileDataId);
                        return file == null ? null : file.Body;
                    }
                }
                else
                {
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                }
            }
            catch (Exception ex)
            {
                // Return any unexpected error in the result
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }
            finally
            {
                // Dispose of all objects used by the data layer to force it to disconnect
                if (objectsToDisposeOnDisconnect != null)
                {
                    foreach (var disposable in objectsToDisposeOnDisconnect.Where(disposable => disposable != null))
                    {
                        disposable.Dispose();
                    }
                }
            }

        }

        /// <summary>
        /// Save upload file to data store.
        /// </summary>
        /// <param name="fileDataId">Id for upload file data.</param>
        /// <param name="dataStore">Data store for upload file to save.</param>
        /// <param name="fileData">Upload file data</param>
        private static void SaveFileToDataStore(Guid fileDataId, DataStore dataStore, byte[] fileData)
        {
            // Get global data facade.
            GetGlobalDataFacade();

            // Attempt to get the selected XpoProvider
            var xpoProvider =
                _globalFacade.ExecuteQuery(
                    uow => uow.Query<XpoProvider>().Where(p => p.Name == dataStore.DatabaseProvider.ToString()))
                    .FirstOrDefault();
            if (xpoProvider == null)
                throw new FaultException<ServiceFault>(
                    new ServiceFault(string.Format("Unknown provider on store named {0}!", dataStore.DatabaseName)));

            // Get the connection string and add it to the list
            var connectionString = ServiceHelper.GetConnectionString(xpoProvider.Name,
                xpoProvider.HasUserName,
                xpoProvider.HasPassword,
                xpoProvider.IsServerBased,
                dataStore.Server,
                dataStore.DatabaseName, null,
                dataStore.IntegratedSecurity,
                dataStore.UserName,
                dataStore.Password);

            // Attempt to connect to the database without upgrading, to determine if an upgrade is required
            IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;

            try
            {
                // Calling UpdateSchema when the data layer was created with AutoCreateOption.None will throw an error if the database needs an update
                var dataLayer = ServiceHelper.CreateDatabaseDataLayer(connectionString, AutoCreateOption.None,
                    _repositoryStoreAssemblies,
                    out objectsToDisposeOnDisconnect);

                // Generate hase for upload file.
                byte[] hash;
                using (var hasher = MD5.Create())
                {
                    hash = hasher.ComputeHash(fileData);
                }

                // Save file to data store.
                if (dataLayer != null)
                {
                    using (var uow = new UnitOfWork(dataLayer))
                    {
                        new FileData(uow)
                        {
                            Body = fileData,
                            CreatedOn = DateTime.Now,
                            HashHex = hash,
                            Oid = fileDataId,
                            Size = fileData.Length
                        };
                        uow.CommitChanges();
                    }
                }
                else
                {
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                }
            }
            catch (Exception ex)
            {
                // Return any unexpected error in the result
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }
            finally
            {
                // Dispose of all objects used by the data layer to force it to disconnect
                if (objectsToDisposeOnDisconnect != null)
                {
                    foreach (var disposable in objectsToDisposeOnDisconnect.Where(disposable => disposable != null))
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Get data store by file.
        /// </summary>
        /// <param name="fileId">Id of upload file specification.</param>
        /// <returns>Retrived data store for save upload file.</returns>
        private static DataStore GetDataStoreByFile(Guid fileId)
        {
            DataStore dataStore = null;

            // Get repository data facade.
            GetRepositoryDataFacade();

            _repositoryFacade.ExecuteUnitOfWork(
                uow =>
                {
                    // Get file specification by key.
                    var file = new XPQuery<File>(uow).FirstOrDefault(p => p.Oid == fileId);

                    if (file == null)
                    {
                        throw new FaultException<ServiceFault>(new ServiceFault("Illegal operation."));
                    }

                    // Get file extension group from file specification.
                    if (!file.Extension.FileExtensionGroups.IsLoaded)
                    {
                        file.Extension.FileExtensionGroups.Load();
                    }

                    // Get active file group.
                    var fileExtensionGroup = file.Extension.FileExtensionGroups.FirstOrDefault(p => p.IsActive);

                    // Get default datastore, if file extension don't belong to any file extension group
                    if (fileExtensionGroup == null)
                    {
                        dataStore = new XPQuery<DataStore>(uow).FirstOrDefault(p => p.IsDefault);
                    }
                    else
                    {
                        // Get relevant data store.
                        if (!fileExtensionGroup.DataStores.IsLoaded)
                        {
                            fileExtensionGroup.DataStores.Load();
                        }

                        dataStore = fileExtensionGroup.DataStores.FirstOrDefault();
                    }
                });
            return dataStore;
        }

        /// <summary>
        /// Returns a list of connection strings for all data stores.
        /// </summary>
        /// <returns>A list of connection strings.</returns>
        private List<string> GetAllDataStoreConnectionStrings()
        {
            var connectionStrings = new List<string>();

            // Get facades
            var repositoryFacade = GetRepositoryDataFacade();
            var globalFacade = GetGlobalDataFacade();

            // Get a list of all data stores
            var repositoryInfos = repositoryFacade.ExecuteQuery(uow => uow.Query<DataStore>());

            // Update each data store
            foreach (var repositoryInfo in repositoryInfos)
            {
                // Attempt to get the selected XpoProvider
                var xpoProvider = globalFacade.ExecuteQuery(uow => uow.Query<XpoProvider>().Where(p => p.Name == repositoryInfo.DatabaseProvider.ToString())).FirstOrDefault();
                if (xpoProvider == null)
                    throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Unknown provider on store named {0}!", repositoryInfo.DatabaseName)));

                // Get the connection string and add it to the list
                connectionStrings.Add(ServiceHelper.GetConnectionString(xpoProvider.Name, xpoProvider.HasUserName, xpoProvider.HasPassword, xpoProvider.IsServerBased, repositoryInfo.Server, repositoryInfo.DatabaseName, null, repositoryInfo.IntegratedSecurity, repositoryInfo.UserName, repositoryInfo.Password));
            }

            return connectionStrings;
        }

        /// <summary>
        /// Initializes and returns a GlobalFacade connected to the data service only.
        /// </summary>
        /// <returns>A GlobalFacade.</returns>
        private static IGlobalFacade GetGlobalDataFacade()
        {
            if (_globalFacade == null)
                _globalFacade = new GlobalFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser("Service-To-Service")));

            try
            {
                if (_globalFacade != null && !_globalFacade.IsDataConnected)
                    _globalFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Global Facade!", ex));
            }

            return _globalFacade;
        }

        /// <summary>
        /// Initializes and returns a RepositoryFacade connected to the data service only.
        /// </summary>
        /// <returns>A RepositoryFacade.</returns>
        private static IRepositoryFacade GetRepositoryDataFacade()
        {
            if (_repositoryFacade == null)
                _repositoryFacade = new RepositoryFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)), null);

            try
            {
                if (_repositoryFacade != null && !_repositoryFacade.IsDataConnected)
                    _repositoryFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Repository Facade!", ex));
            }

            return _repositoryFacade;
        }

        #endregion
    }
}
