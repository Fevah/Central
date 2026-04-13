using System;
using System.Threading;
using TIG.IntegrationServer.Logging.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Core;
using TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatcher.Interface;

namespace TIG.IntegrationServer.SyncEngine.Custom.Context.Dispatchers
{
    public class CancellationDispatcher : SubmittableDispatcherBase, ICancellationDispatcher
    {
        #region Private Fields

        private readonly ManualResetEvent _canceledEvent = new ManualResetEvent(true);

        private bool _cancellationRequested;
        private bool _disposed;

        #endregion


        #region Constructors

        /// <summary>
        /// Constructor with component
        /// </summary>
        /// <param name="log">Log writter</param>
        public CancellationDispatcher(ILog log)
            : base(log)
        {
        }

        #endregion


        #region ICancellationDispatcher Members

        /// <summary>
        /// CancellationRequested indicate system submitted cancellation 
        /// </summary>
        public bool CancellationRequested
        {
            get
            {
                RunDisposedCheck();

                var cancellationRequested = false;
                PerformReadOperation(() =>
                {
                    cancellationRequested = _cancellationRequested;
                });
                return cancellationRequested;
            }
        }

        /// <summary>
        /// Request cancellation
        /// </summary>
        public void RequestCancellation()
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                _cancellationRequested = true;

                if (AllSubmittersConfirmedSubmission)
                {
                    return;
                }

                _canceledEvent.Reset();
            });
        }

        /// <summary>
        /// Wait for cancellation submission
        /// </summary>
        public void WaitForCancellationSubmission()
        {
            RunDisposedCheck();

            _canceledEvent.WaitOne();
        }

        /// <summary>
        /// Register cancellation submitter
        /// </summary>
        /// <param name="submitter">Submitter to be register cancellation</param>
        public bool RegisterCancellationSubmitter(object submitter)
        {
            RunDisposedCheck();

            var result = false;

            PerformWriteOperation(() =>
            {
                if (_cancellationRequested)
                {
                    result = false;
                    return;
                }

                RegisterSubmitter(submitter);
                result = true;
            });

            return result;
        }

        /// <summary>
        /// Submit cancellation submitter
        /// </summary>
        /// <param name="submitter">Submitter was registered cancellation</param>
        public void SubmitCancellation(object submitter)
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                Submit(submitter);

                if (_cancellationRequested && AllSubmittersConfirmedSubmission)
                {
                    _canceledEvent.Set();
                }
            });
        }

        /// <summary>
        /// Unregister cancellation submitter
        /// </summary>
        /// <param name="submitter">Submitter to be unregister cancellation</param>
        public void UnregisterCancellationSubmitter(object submitter)
        {
            RunDisposedCheck();

            PerformWriteOperation(() =>
            {
                UnregisterSubmitter(submitter);

                if (_cancellationRequested && AllSubmittersConfirmedSubmission)
                {
                    _canceledEvent.Set();
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

            if (_canceledEvent != null)
            {
                _canceledEvent.Dispose();
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
