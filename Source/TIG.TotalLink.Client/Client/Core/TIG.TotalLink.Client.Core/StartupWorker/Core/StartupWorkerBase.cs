using System.ComponentModel;
using System.Threading;

namespace TIG.TotalLink.Client.Core.StartupWorker.Core
{
    /// <summary>
    /// Base class for BackgroundWorkers that are used to initialize the application on startup and report progress to the splash screen.
    /// </summary>
    public abstract class StartupWorkerBase : BackgroundWorker
    {
        #region Private Fields

        private bool _rethrowErrors = true;

        #endregion


        #region Constructors

        protected StartupWorkerBase()
        {
            // Apply settings to the base BackgroundWorker
            base.WorkerSupportsCancellation = false;
            base.WorkerReportsProgress = true;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The number of steps that are required to complete this startup worker.
        /// A value should be assigned to this property in Initialize.
        /// </summary>
        public int Steps { get; protected set; }

        /// <summary>
        /// The number of milliseconds to delay each step of the startup workers.
        /// </summary>
        public int StepDelay { get; set; }

        /// <summary>
        /// Indicates if errors in the startup worker should be re-thrown to the default error handlers.
        /// Set this to false if errors will be handled manually.
        /// </summary>
        public bool RethrowErrors
        {
            get { return _rethrowErrors; }
            set { _rethrowErrors = value; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Initializes the startup worker.
        /// Inheriters must override this and calculate an appropriate value for the Steps property.
        /// </summary>
        public abstract void Initialize();

        #endregion


        #region Overrides

        public new bool WorkerSupportsCancellation
        {
            get { return base.WorkerSupportsCancellation; }
        }

        public new bool WorkerReportsProgress
        {
            get { return base.WorkerReportsProgress; }
        }

        public new void ReportProgress(int percentProgress)
        {
            ReportProgress(percentProgress, null);
        }

        public new void ReportProgress(int percentProgress, object userState)
        {
            base.ReportProgress(percentProgress, userState);

            if (StepDelay > 0)
                Thread.Sleep(StepDelay);
        }

        #endregion
    }
}
