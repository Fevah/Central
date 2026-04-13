using System;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.Security.Cryptography;
using TIG.IntegrationServer.Security.Cryptography.MD5;
using TIG.IntegrationServer.SyncEngine.Core.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Configuration.Interface;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context
{
    public class Context : SyncEngineComponent, IContext
    {
        #region Private Fields

        private bool _disposed;

        #endregion


        #region IContext Members

        /// <summary>
        /// Configuration for handle configuration persistence logic
        /// </summary>
        public IConfiguration Configuration { get; private set; }

        /// <summary>
        /// PauseDispatcher for handle pause logic
        /// </summary>
        public IPauseDispatcher PauseDispatcher { get; private set; }

        /// <summary>
        /// CancellationDispatcher for handle cancellation logic
        /// </summary>
        public ICancellationDispatcher CancellationDispatcher { get; private set; }

        /// <summary>
        /// HashMaster to handle encrypt things
        /// </summary>
        public IHashMaster HashMaster { get; private set; }

        /// <summary>
        /// SyncStatusManager for handle sync status to stop flow back
        /// </summary>
        public ISyncStatusManager SyncStatusManager { get; private set; }

        #endregion


        #region Constructors

        /// <summary>
        /// Default Consturctor with required components.
        /// </summary>
        /// <param name="log">Log witter</param>
        /// <param name="configuration">Configuration for sync logic</param>
        /// <param name="pauseDispatcher">Pause Dispatcher</param>
        /// <param name="cancellationDispatcher">Cancellation Dispatcher</param>
        /// <param name="hashMaster">Hash Master</param>
        /// <param name="statusManager">Sync StatusManager</param>
        public Context(
            ILog log,
            IConfiguration configuration,
            IPauseDispatcher pauseDispatcher,
            ICancellationDispatcher cancellationDispatcher,
            Md5HashMaster hashMaster,
            ISyncStatusManager statusManager)
            : base(log)
        {
            Configuration = configuration;
            PauseDispatcher = pauseDispatcher;
            CancellationDispatcher = cancellationDispatcher;
            HashMaster = hashMaster;
            SyncStatusManager = statusManager;
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Dispose all relative objects
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (Configuration != null)
            {
                Configuration.Dispose();
            }

            if (PauseDispatcher != null)
            {
                PauseDispatcher.Dispose();
            }

            if (CancellationDispatcher != null)
            {
                CancellationDispatcher.Dispose();
            }

            _disposed = true;
        }

        /// <summary>
        /// RunDisposedCheck for check current was disposed or not.
        /// </summary>
        protected override void RunDisposedCheck()
        {
            base.RunDisposedCheck();

            if (_disposed)
            {
                throw new ObjectDisposedException(this.GetType().FullName);
            }
        }

        #endregion
    }
}
