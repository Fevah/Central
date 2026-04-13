using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Core.Interface.BackgroundService;
using TIG.TotalLink.Client.Module.Repository.DataModel;
using TIG.TotalLink.Shared.DataModel.Repository;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Client.Module.Repository.BackgroundService
{
    public class SyncBackgroundService : ISyncBackgroundService
    {
        #region Private Properties

        private readonly IRepositoryFacade _repositoryFacade;
        private readonly ConcurrentQueue<dynamic> _syncQueue = new ConcurrentQueue<dynamic>();
        private readonly ManualResetEvent _manualResetEvent;
        private int _maxTaskInPool = 5;
        private volatile int _seed;

        #endregion


        #region Properties

        /// <summary>
        /// Sync items
        /// </summary>
        public ObservableCollection<ISyncEntity> SyncEntities { get; private set; }

        #endregion


        #region Constructors

        /// <summary>
        /// Default Construction with repository facade.
        /// </summary>
        /// <param name="repositoryFacade">Repository facade</param>
        public SyncBackgroundService(IRepositoryFacade repositoryFacade)
        {
            SyncEntities = new ObservableCollection<ISyncEntity>();
            _repositoryFacade = repositoryFacade;
            _manualResetEvent = new ManualResetEvent(false);
            var syncTaskCheckThread = new Thread(SyncTaskCheck)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            syncTaskCheckThread.Start();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Add to be update file to uploade queue.
        /// </summary>
        /// <param name="fileId">FileId of file specification</param>
        /// <param name="fileDataId">File data id</param>
        /// <param name="syncMode">Indicate sync is download or upload</param>
        public void Enqueue(Guid fileId, Guid fileDataId, SyncMode syncMode)
        {
            _syncQueue.Enqueue(new { fileId, fileDataId, syncMode });
            _manualResetEvent.Set();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Check new upload task
        /// </summary>
        private void SyncTaskCheck()
        {
            _seed = 0;
            while (true)
            {
                dynamic fileInfo;
                // If task queue don't have any task need to upload and all of 5 task has running, then sleep check.
                if (!_syncQueue.TryDequeue(out fileInfo)
                    || _seed > 5)
                {
                    _manualResetEvent.Reset();
                    _manualResetEvent.WaitOne();
                    continue;
                }

                _seed++;

                Guid fileId = fileInfo.fileId;
                Guid fileDataId = fileInfo.fileDataId;
                SyncMode syncMode = fileInfo.syncMode;

                var file = _repositoryFacade.ExecuteQuery(uow => uow.Query<File>().Where(p => p.Oid == fileId)).First();
                var fileName = file.Name;


                // Create a new task for file upload.
                try
                {
                    Task.Run(() =>
                    {
                        switch (syncMode)
                        {
                            case SyncMode.Upload:
                                ProcessUpload(fileDataId, fileId, fileName);
                                break;
                            case SyncMode.Download:
                                ProcessDownload(fileId, fileDataId, fileName);
                                break;
                        }

                        _seed--;
                    });
                }
                catch (Exception)
                {
                    // Put file to upload queue and try it again.
                    _syncQueue.Enqueue(fileInfo);

                    // TODO: System need write error to log system.
                    throw;
                }
            }
        }

        /// <summary>
        /// Process download
        /// </summary>
        /// <param name="fileId">FileId of file specification</param>
        /// <param name="fileDataId">File data id</param>
        /// <param name="fileName">Name of download file</param>
        private async void ProcessDownload(Guid fileId, Guid fileDataId, string fileName)
        {
            // File download initialize.
            var fileSize = await _repositoryFacade.FileDownloadInitializeAsync(fileId, fileDataId);

            var downloader = new Downloader(_repositoryFacade, fileDataId, fileSize)
            {
                FileName = fileName
            };

            SyncEntities.Add(downloader);
            await downloader.Download();

            // If download stoped, next process will stop.
            if (downloader.Status != SyncStatus.Finish)
            {
                SyncEntities.Remove(downloader);
                return;
            }

            // Get file hash.
            byte[] hash;
            using (var hasher = MD5.Create())
            {
                hash = hasher.ComputeHash(downloader.File);
            }

            // Save file to local database.
            await _repositoryFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                new FileData(uow)
                {
                    Oid = fileDataId,
                    Body = downloader.File,
                    Size = fileSize,
                    CreatedOn = DateTime.Now,
                    Version = 0,
                    HashHex = hash
                };
            }, ServiceTypes.LocalData);

            if (downloader.Status == SyncStatus.Finish)
            {
                SyncEntities.Remove(downloader);
            }
        }

        /// <summary>
        /// Process Upload
        /// </summary>
        /// <param name="fileId">FileId of file specification</param>
        /// <param name="fileDataId">File data id</param>
        /// <param name="fileName">Name of upload file</param>
        private async void ProcessUpload(Guid fileDataId, Guid fileId, string fileName)
        {
            var uploader = new Uploader(_repositoryFacade, fileDataId)
            {
                FileName = fileName
            };
            SyncEntities.Add(uploader);
            await uploader.Upload();

            // If upload stoped, next process will stop.
            if (uploader.Status != SyncStatus.Finish)
            {
                SyncEntities.Remove(uploader);
                return;
            }

            await _repositoryFacade.FileUploadDoneAsync(fileId, fileDataId);

            if (uploader.Status == SyncStatus.Finish)
            {
                SyncEntities.Remove(uploader);
            }
        }

        #endregion
    }
}