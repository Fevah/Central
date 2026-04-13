using System;
using System.IO;
using System.Threading;
using System.Windows;
using DevExpress.Mvvm;
using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Core;
using DevExpress.Xpo;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.StartupWorker;
using TIG.TotalLink.Client.Core.StartupWorker.Core;
using TIG.TotalLink.Client.Core.View;

namespace TIG.TotalLink.Client.Core
{
    public abstract class BootstrapperBase
    {
        #region Private Fields

        private StartupWorkerManager _startupWorkerManager;
        private System.Windows.Window _mainWindow;

        #endregion


        #region Constructors

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="commandLineArgs">Arguments that were passed on the command line.</param>
        protected BootstrapperBase(string[] commandLineArgs)
        {
        }

        #endregion


        #region Protected Properties

        /// <summary>
        /// Specifies a delay (miliseconds) to occur before performing each startup worker step.
        /// </summary>
        protected int DelayStartup { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Begins initialization of the application.
        /// </summary>
        public virtual void RunStartup()
        {
            // Show the splash screen
            DXSplashScreen.Show<SplashScreenView>();
            DXSplashScreen.SetState("Loading...");
            Thread.Sleep(100);

            // Apply the saved theme
            ThemeManager.ApplicationThemeName = AppContextViewModel.Instance.ThemeName;

            // Replace the default Messenger with one that supports multi-threading
            Messenger.Default = new Messenger(true);

            // By default, XPO will mark objects to save even when only non-persistent properties were changed
            // Setting this property will globally make all sessions only write when persistent properties are changed
            XpoDefault.IsObjectModifiedOnNonPersistentPropertyChange = false;

            // Create and run the startup workers
            RunStartupWorkers();
        }

        #endregion


        #region Protected Methods

        /// <summary>
        /// Called when all startup tasks are complete and the main application should be started.
        /// </summary>
        protected virtual void RunApplication()
        {
            // Update the splash screen
            UpdateSplashScreen(_startupWorkerManager.TotalSteps, "Starting...");
        }

        /// <summary>
        /// Shows the main window.
        /// </summary>
        /// <param name="mainWindowName">The name of the window to use as the main window.</param>
        protected virtual void ShowMainWindow(string mainWindowName)
        {
            // Use the view locator to find the the main window
            _mainWindow = ViewLocator.Default.ResolveView(mainWindowName) as System.Windows.Window;
            if (_mainWindow == null)
                throw new Exception("Failed to create main window!");

            // Show the main window
            Application.Current.MainWindow = _mainWindow;
            _mainWindow.Loaded += MainWindow_Loaded;
            _mainWindow.Closed += MainWindow_Closed;
            _mainWindow.Show();
        }

        /// <summary>
        /// Called when the main window has finished loading.
        /// </summary>
        protected virtual void OnMainWindowLoaded()
        {
        }

        /// <summary>
        /// Inheriters can override this to add additional startup workers to the queue.
        /// </summary>
        /// <param name="startupWorkerManager">The StartupWorkerManager that contains the startup queue.</param>
        protected virtual void EnqueueStartupWorkers(StartupWorkerManager startupWorkerManager)
        {
        }

        /// <summary>
        /// Updates details on the splash screen.
        /// </summary>
        /// <param name="stepsCompleted">The number of steps that have been completed in the current worker.</param>
        /// <param name="message">The message to display.</param>
        protected void UpdateSplashScreen(int stepsCompleted, string message)
        {
            // Abort if the splash screen isn't open
            if (!DXSplashScreen.IsActive)
                return;

            // Calculate the current progress as a percentage
            // (We add one extra step to the total to account for showing the first window)
            var progressPercentage = stepsCompleted / (_startupWorkerManager.TotalSteps + 1D) * 100D;

            // Update the splash screen
            DXSplashScreen.Progress(progressPercentage);
            DXSplashScreen.SetState(message);
        }

        /// <summary>
        /// Closes the splash screen.
        /// </summary>
        protected void CloseSplashScreen()
        {
            // Abort if the splash screen isn't open
            if (!DXSplashScreen.IsActive)
                return;

            // Close the splash screen
            DXSplashScreen.Progress(100);
            DXSplashScreen.Close();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Queues up workers for each startup process that needs to occur, and starts them.
        /// </summary>
        private void RunStartupWorkers()
        {
            // Create a StartupWorkerManager
            _startupWorkerManager = new StartupWorkerManager
            {
                StepDelay = DelayStartup,
                UpdateProgress = StartupWorkerManager_UpdateProgress,
                Completed = StartupWorkerManager_Completed
            };

            // Queue up workers for each startup process that needs to occur
            _startupWorkerManager.Enqueue(new PreLoadAssembliesStartupWorker(AppDomain.CurrentDomain.BaseDirectory, "Loading built-ins... {0}", "DevExpress.Xpf.Themes"));
            _startupWorkerManager.Enqueue(new PreLoadAssembliesStartupWorker(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Module"), "Loading modules... {0}"));

            // Allow derived classes to add startup workers
            EnqueueStartupWorkers(_startupWorkerManager);

            // Set the progress to zero
            DXSplashScreen.Progress(0);

            // Start the workers
            _startupWorkerManager.Run();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Called by the StartupWorkerManager when progress has changed.
        /// </summary>
        /// <param name="stepscompleted">The number of steps that have been completed so far.</param>
        /// <param name="message">The message to display.</param>
        private void StartupWorkerManager_UpdateProgress(int stepscompleted, string message)
        {
            UpdateSplashScreen(stepscompleted, message);
        }

        /// <summary>
        /// Called by the StartupWorkerManager when all workers are completed.
        /// </summary>
        private void StartupWorkerManager_Completed()
        {
            RunApplication();
        }

        /// <summary>
        /// Handles the Loaded event for the main window.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OnMainWindowLoaded();

            // Activate the main window
            var window = sender as System.Windows.Window;
            if (window == null) return;
            window.Activate();
        }

        /// <summary>
        /// Handles the Closed event for the main window.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Save user settings
            AppContextViewModel.Instance.SaveSettings();

            // Make sure the application shuts down when the main window is closed
            Application.Current.Shutdown();
        }

        #endregion
    }
}
