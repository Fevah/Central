using System;
using System.Linq;
using System.Threading.Tasks;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Client.Module.Repository.DataModel;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Client.Module.Repository.BackgroundService
{
    public class Uploader : SyncEntityBase
    {
        #region Private Methods

        private const int BufferSize = 1024 * 16;
        private readonly IRepositoryFacade _repositoryFacade;
        private readonly FileData _file;
        private long _offset;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Uploader()
        {
        }

        /// <summary>
        /// Constructor to initialize uploader.
        /// </summary>
        /// <param name="repositoryFacade">Repository facade</param>
        /// <param name="fileDataId">Id of upload file data.</param>
        public Uploader(IRepositoryFacade repositoryFacade, Guid fileDataId)
        {
            _offset = 0;
            _repositoryFacade = repositoryFacade;
            if (!_repositoryFacade.IsLocalDataConnected)
            {
                _repositoryFacade.Connect(ServiceTypes.LocalData);
            }

            // Get file data from local cache.
            _file = _repositoryFacade.ExecuteQuery(uow => new XPQuery<FileData>(uow).Where(p => p.Oid == fileDataId),
                    ServiceTypes.LocalData).FirstOrDefault();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Upload file
        /// </summary>
        public async Task Upload()
        {
            long retryOffset = 0;
            var retryTimes = 0;

            FileSize = _file.Body.Length;

            Status = SyncStatus.Sync;

            while (_offset < FileSize)
            {
                if (Status == SyncStatus.Stop)
                {
                    _offset = 0;
                    return;
                }

                if (Status == SyncStatus.Pause)
                {
                    ManualResetEvent.Reset();
                    ManualResetEvent.WaitOne();
                }

                if (_file == null)
                {
                    return;
                }

                // Take chuck of upload file.
                var buffer = _file.Body.Skip((int)_offset).Take(BufferSize).ToArray();

                try
                {
                    await _repositoryFacade.FileChunkUploadAsync(_file.Oid, (int)_offset, buffer);
                }
                catch (Exception ex)
                {
                    if (retryOffset == Progress
                        && retryTimes > 5)
                    {
                        Status = SyncStatus.Error;
                        var serviceException = new ServiceExceptionHelper(ex);
                        ErrorMessage = string.Format("Download file failed after retry 5 times.\n{0}", serviceException.Message);
                        return;
                    }

                    retryOffset = _offset;
                    // Anything wrong, downloader will wait for 5 secends and try again.
                    Task.Delay(1000 * 5);
                    retryTimes++;
                    continue;
                }

                _offset += buffer.Length;
                SetProgress(_offset);
            }

            Status = SyncStatus.Finish;
        }

        #endregion
    }
}