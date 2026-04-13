using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TIG.TotalLink.Client.Core.StartupWorker.Core
{
    /// <summary>
    /// Manages and runs startup workers.
    /// </summary>
    public class StartupWorkerManager
    {
        #region Public Enums

        public enum States
        {
            Inactive,
            Active,
            Complete
        }

        #endregion


        #region Public Delegates

        public delegate void UpdateProgressDelegate(int stepsCompleted, string message);
        public delegate void CompletedDelegate();

        #endregion


        #region Private Fields

        private readonly Queue<StartupWorkerBase> _startupWorkers = new Queue<StartupWorkerBase>();
        private int _stepsCompleted;

        #endregion


        #region Public Properties

        /// <summary>
        /// State of the manager.
        /// </summary>
        public States State { get; private set; }

        /// <summary>
        /// Total steps that are required to complete all workers.
        /// </summary>
        public int TotalSteps { get; private set; }

        /// <summary>
        /// Specifies a delay (miliseconds) to occur before performing each step.
        /// </summary>
        public int StepDelay { get; set; }

        /// <summary>
        /// Method to call when progress changes.
        /// </summary>
        public UpdateProgressDelegate UpdateProgress { get; set; }

        /// <summary>
        /// Method to call when all workers are complete.
        /// </summary>
        public CompletedDelegate Completed { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Adds a startup worker to the queue of workers to run.
        /// Only one worker will be active at a time, and workers will be processed in the order they are added.
        /// </summary>
        /// <param name="startupWorker">The startup worker to add to the queue.</param>
        public void Enqueue(StartupWorkerBase startupWorker)
        {
            _startupWorkers.Enqueue(startupWorker);
        }

        /// <summary>
        /// Starts running workers in the queue.
        /// </summary>
        public void Run()
        {
            // Abort if the manager is not inactive
            if (State != States.Inactive)
                return;

            State = States.Active;

            // Initialize all the startup workers and calculate the total steps
            foreach (var startupWorker in _startupWorkers)
            {
                startupWorker.Initialize();
                startupWorker.StepDelay = StepDelay;
                TotalSteps += startupWorker.Steps;
            }

            // Run the first startup worker
            RunNextStartupWorker();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Executes the next startup worker in the queue, or shows the main window if all workers are complete.
        /// </summary>
        private void RunNextStartupWorker()
        {
            if (_startupWorkers.Count > 0)
            {
                // Collect the next startup worker
                var startupWorker = _startupWorkers.Dequeue();

                // Attach events
                startupWorker.ProgressChanged += StartupWorker_ProgressChanged;
                startupWorker.RunWorkerCompleted += StartupWorker_RunWorkerCompleted;

                // Run the startup worker
                startupWorker.RunWorkerAsync();
            }
            else
            {
                // There are no workers left, so call the Completed delegate
                State = States.Complete;
                if (Completed != null)
                    Completed();
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the ProgressChanged event for all startup workers.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void StartupWorker_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            // Call the UpdateProgress delegate
            if (UpdateProgress != null)
                UpdateProgress(_stepsCompleted + e.ProgressPercentage, e.UserState as string);
        }

        /// <summary>
        /// Handles the RunWorkerCompleted event for all startup workers.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void StartupWorker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            // Get the sender as a StartupWorker
            var startupWorker = sender as StartupWorkerBase;
            if (startupWorker == null)
                return;
            
            // Detach events
            startupWorker.ProgressChanged -= StartupWorker_ProgressChanged;
            startupWorker.RunWorkerCompleted -= StartupWorker_RunWorkerCompleted;

            // Add the steps from the worker that just finished to the total steps completed
            _stepsCompleted += startupWorker.Steps;

            // If the startup worker generated an error, rethrow it
            if (startupWorker.RethrowErrors && e.Error != null)
            {
                // If the error contained a ReflectionTypeLoadException, extract the useful details
                var typeLoadException = e.Error.InnerException as ReflectionTypeLoadException;
                if (typeLoadException != null)
                    throw new Exception(string.Format("{0}\r\n\r\n{1}", e.Error.Message, string.Join("\r\n", typeLoadException.LoaderExceptions.Select(l => l.Message))));

                // Otherwise, rethrow the original exception
                throw e.Error;
            }
            
            // Run the next startup worker
            RunNextStartupWorker();
        }

        #endregion
    }
}
