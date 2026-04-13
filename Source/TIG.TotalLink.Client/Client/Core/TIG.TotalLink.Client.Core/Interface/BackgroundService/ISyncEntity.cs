using System.ComponentModel;
using TIG.TotalLink.Client.Core.Enum;

namespace TIG.TotalLink.Client.Core.Interface.BackgroundService
{
    public interface ISyncEntity : INotifyPropertyChanged
    {
        /// <summary>
        /// Status of sync item
        /// </summary>
        SyncStatus Status { get; set; }

        /// <summary>
        /// Sync mode of sync item
        /// </summary>
        SyncMode Mode { get; set; }

        /// <summary>
        /// Name of sync item
        /// </summary>
        string FileName { get; set; }

        /// <summary>
        /// Size of sync file
        /// </summary>
        long FileSize { get; set; }

        /// <summary>
        /// Progress of sync item
        /// </summary>
        long Progress { get; }

        /// <summary>
        /// Error message of sync item
        /// </summary>
        string ErrorMessage { get; set; }

        /// <summary>
        /// Pause method for pause sync item
        /// </summary>
        void Pause();

        /// <summary>
        /// Stop method for stop sync item
        /// </summary>
        void Stop();

        /// <summary>
        /// Start method for start sync item
        /// </summary>
        void Start();
    }
}