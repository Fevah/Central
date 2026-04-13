using System;
using System.Collections.ObjectModel;
using TIG.TotalLink.Client.Core.Enum;

namespace TIG.TotalLink.Client.Core.Interface.BackgroundService
{
    public interface ISyncBackgroundService
    {
        /// <summary>
        /// Add to be update file to uploade queue.
        /// </summary>
        /// <param name="fileId">FileId of file specification</param>
        /// <param name="fileDataId">File data id</param>
        /// <param name="syncMode">Indicate sync is download or upload</param>
        void Enqueue(Guid fileId, Guid fileDataId, SyncMode syncMode);

        /// <summary>
        /// Sync items
        /// </summary>
        ObservableCollection<ISyncEntity> SyncEntities { get; }
    }
}