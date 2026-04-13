using System;
using System.Threading;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatchers
{
    public class PauseDispatcher : SubmittableDispatcherBase, IPauseDispatcher
    {
        #region Private Fields

        private readonly ManualResetEvent _pauseRevokedEvent = new ManualResetEvent(true);
        private readonly ManualResetEvent _pausedEvent = new ManualResetEvent(true);

        private bool _pauseRequested;
        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with log
        /// </summary>
        /// <param name="log">Log writter</param>
        public PauseDispatcher(ILog log)
            : base(log)
        {
        }

        #endregion


        #region IPauseDispatcher Members

        /// <summary>
        /// PauseRequested for indicate system required pause
        /// </summary>
        public bool PauseRequested
        {
            get
            {
                RunDisposedCheck();

                var pauseRequested = false;
                PerformReadOperation(() =>
                {
                    pauseRequested = _pauseRequested;
                });
                return pauseRequested;
            }
        }

        /// <summary>
        /// Request pause
        /// </summary>
        public void RequestPause()
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                _pauseRequested = true;
                _pauseRevokedEvent.Reset();
                _pausedEvent.Reset();
            });
        }

        /// <summary>
        /// Wait for pause submission
        /// </summary>
        public void WaitForPauseSubmission()
        {
            RunDisposedCheck();

            _pausedEvent.WaitOne();
        }

        /// <summary>
        /// Wait until pause revoked
        /// </summary>
        public void WaitUntilPauseRevoked()
        {
            RunDisposedCheck();

            _pauseRevokedEvent.WaitOne();
        }

        /// <summary>
        /// Revoke pause request
        /// </summary>
        public void RevokePauseRequest()
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                _pauseRequested = false;
                _pauseRevokedEvent.Set();
                RevokeSubmittions();
            });

        }

        /// <summary>
        /// Register pause submitter
        /// </summary>
        /// <param name="submitter">Submitter to be register pause</param>
        public void RegisterPauseSubmitter(object submitter)
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                RegisterSubmitter(submitter);
            });
        }

        /// <summary>
        /// Submit pause
        /// </summary>
        /// <param name="submitter">Submitter was register pause</param>
        public void SubmitPause(object submitter)
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                Submit(submitter);

                if (_pauseRequested && AllSubmittersConfirmedSubmission)
                {
                    _pausedEvent.Set();
                }
            });
        }

        /// <summary>
        /// Unregeister pause submitter
        /// </summary>
        /// <param name="submitter">Submitter to be unregister</param>
        public void UnregisterPauseSubmitter(object submitter)
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                UnregisterSubmitter(submitter);

                if (_pauseRequested && AllSubmittersConfirmedSubmission)
                {
                    _pausedEvent.Set();
                }
            });
        }

        #endregion


        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (_pauseRevokedEvent != null)
            {
                _pauseRevokedEvent.Dispose();
            }

            if (_pausedEvent != null)
            {
                _pausedEvent.Dispose();
            }

            _disposed = true;
        }

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
