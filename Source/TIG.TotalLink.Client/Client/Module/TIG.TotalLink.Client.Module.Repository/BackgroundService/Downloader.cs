using System;
using System.Threading.Tasks;
using TIG.TotalLink.Client.Core.Attribute;
using TIG.TotalLink.Client.Core.Enum;
using TIG.TotalLink.Shared.Facade.Core.Helper;
using TIG.TotalLink.Shared.Facade.Repository;

namespace TIG.TotalLink.Client.Module.Repository.BackgroundService
{
    public class Downloader : SyncEntityBase
    {
        #region Private Methods

        private const int BufferSize = 1024 * 16;
        private readonly IRepositoryFacade _repositoryFacade;
        private readonly Guid _fileDataId;
        private byte[] _file;
        private long _offset;

        #endregion


        #region Properties

        /// <summary>
        /// File for download from server.
        /// </summary>
        [DoNotCopy]
        public byte[] File
        {
            get { return _file; }
            set
            {
                _file = value;
                RaisePropertyChanged(() => File);
            }
        }

        #endregion


        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Downloader()
        {
        }

        /// <summary>
        /// Constructor to initialize downloader.
        /// </summary>
        /// <param name="repositoryFacade">Repository facade</param>
        /// <param name="fileDataId">Id of download file data.</param>
        /// <param name="fileSize">Size of download file</param>
        public Downloader(IRepositoryFacade repositoryFacade, Guid fileDataId, int fileSize)
        {
            _offset = 0;
            _repositoryFacade = repositoryFacade;
            _fileDataId = fileDataId;

            FileSize = fileSize;

            File = new byte[fileSize];

            if (!_repositoryFacade.IsConnected)
            {
                _repositoryFacade.Connect();
            }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// download file
        /// </summary>
        public async Task Download()
        {
            long retryOffset = 0;
            var retryTimes = 0;

            Status = SyncStatus.Sync;

            while (_offset <= FileSize)
            {
                // If status is stop, stop loop.
                if (Status == SyncStatus.Stop)
                {
                    _offset = 0;
                    File = null;
                    return;
                }

                // Hang on thread, if status is pause.
                if (Status == SyncStatus.Pause)
                {
                    ManualResetEvent.Reset();
                    ManualResetEvent.WaitOne();
                }

                try
                {
                    var chunk = await _repositoryFacade.FileChunkDownloadAsync(_fileDataId, (int)_offset);
                    chunk.CopyTo(File, _offset);
                }
                catch (Exception ex)
                {
                    // If it is same chunk and retry times reach to 5, download will be mark to failed. 
                    if (retryOffset == _offset
                        && retryTimes > 5)
                    {
                        Status = SyncStatus.Error;
                        File = null;
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

                _offset += BufferSize;
                SetProgress(_offset);
            }

            Status = SyncStatus.Finish;
        }

        #endregion
    }
}