using System;
using TIG.IntegrationServer.Logging.Core;

namespace TIG.IntegrationServer.SyncEngine.Custom
{
    public abstract class SyncEngineComponent : IDisposable
    {
        #region Protected Fields

        protected readonly ILog Log;

        #endregion


        #region Private Fields

        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Default Consturctor with log
        /// </summary>
        /// <param name="log">Log writter</param>
        protected SyncEngineComponent(ILog log)
        {
            Log = log;
        }

        #endregion


        #region Destructors

        ~SyncEngineComponent()
        {
            Dispose(false);
        }
        
        #endregion


        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        protected virtual void RunDisposedCheck()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        #endregion
    }
}
