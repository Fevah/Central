using System;

namespace TIG.IntegrationServer.SyncEngine.Core.Interface
{
    public interface ISyncMachine : IDisposable
    {
        /// <summary>
        /// Start sync machine
        /// </summary>
        void Start();

        /// <summary>
        /// Pause sync machine
        /// </summary>
        void Pause();

        /// <summary>
        /// Continue sync machine when paused sync machine.
        /// </summary>
        void Continue();

        /// <summary>
        /// Stop sync machine
        /// </summary>
        void Stop();
    }
}
