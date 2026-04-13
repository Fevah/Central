using System;
using System.ComponentModel;
using System.Windows;
using CommandLine;
using DevExpress.Mvvm.UI;
using MonitoredUndo;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.StartupWorker.Core;
using TIG.TotalLink.Client.Editor.StartupWorker;
using TIG.TotalLink.Client.StartupWorker;
using TIG.TotalLink.Client.Undo.Core;
using TIG.TotalLink.Client.ViewModel;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Client
{
    public class Bootstrapper : BootstrapperBase
    {
        #region Private Fields

        private readonly CommandLineOptions _commandLineOptions = new CommandLineOptions();
        private System.Windows.Window _loginWindow;
        private Exception _loginError;

        #endregion


        #region Constructors

        /// <summary>
        /// Public constructor.
        /// </summary>
        /// <param name="commandLineArgs">Arguments that were passed on the command line.</param>
        public Bootstrapper(string[] commandLineArgs)
            : base(commandLineArgs)
        {
            // Parse the command line arguments
            Parser.Default.ParseArguments(commandLineArgs, _commandLineOptions);

            DelayStartup = _commandLineOptions.DelayStartup;
            // Set datagrid control working on multi threads.
            DevExpress.Xpf.Core.DXGridDataController.DisableThreadingProblemsDetection = true;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Shows the login window.
        /// </summary>
        private void ShowLoginWindow()
        {
            // Use the view locator to find the the login window
            _loginWindow = (ViewLocator.Default.ResolveView("LoginWindow") as System.Windows.Window);
            if (_loginWindow == null)
                throw new Exception("Failed to create login window!");

            // Use the view locator to find the the login viewmodel
            var loginViewModel = (ViewLocator.Default.ResolveView("LoginViewModel") as LoginViewModel);
            if (loginViewModel == null)
                throw new Exception("Failed to create login window!");

            // Copy parameters from the command line options to the login window
            if (_commandLineOptions.Auth != CommandLineOptions.AuthMethods.Unspecified)
            {
                if (_commandLineOptions.Auth == CommandLineOptions.AuthMethods.Manual)
                    loginViewModel.AuthMethod = LoginViewModel.AuthMethods.TotalLink;
                else
                    loginViewModel.AuthMethod = (LoginViewModel.AuthMethods)_commandLineOptions.Auth;
            }
            if (!string.IsNullOrWhiteSpace(_commandLineOptions.Server))
                loginViewModel.Server = _commandLineOptions.Server;
            if (_commandLineOptions.BasePort.HasValue)
                loginViewModel.BasePort = _commandLineOptions.BasePort.Value;
            if (!string.IsNullOrWhiteSpace(_commandLineOptions.Username))
                loginViewModel.User = _commandLineOptions.Username;

            // If an automatic login was attempted and failed, show the error on the login window
            if (_loginError != null)
            {
                var serviceException = new ServiceExceptionHelper(_loginError);
                loginViewModel.LoginErrorMessage = serviceException.Message;
            }

            // Show the login window
            _loginWindow.Loaded += LoginWindow_Loaded;
            _loginWindow.Closed += LoginWindow_Closed;
            AppContextViewModel.Instance.PropertyChanged += AppContextViewModel_PropertyChanged;
            _loginWindow.Show();
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the RunWorkerCompleted event for the LoginStartupWorker.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LoginWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var loginWorker = (LoginStartupWorker)sender;
            loginWorker.RunWorkerCompleted -= LoginWorker_RunWorkerCompleted;
            _loginError = e.Error;
        }

        /// <summary>
        /// Handles the Loaded event for the login window.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Stop handling events
            _loginWindow.Loaded -= LoginWindow_Loaded;

            // Close the splash screen
            CloseSplashScreen();

            // Activate the login window
            var window = sender as System.Windows.Window;
            if (window == null) return;
            window.Activate();
        }

        /// <summary>
        /// Handles the Closed event for the login window.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void LoginWindow_Closed(object sender, EventArgs e)
        {
            // Save user settings
            // (In case the login window state has changed and the main window will never be shown)
            AppContextViewModel.Instance.SaveSettings();
        }

        /// <summary>
        /// Handles the PropertyChanged event for the AppContextViewModel.
        /// </summary>
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void AppContextViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // We are only interested in the AuthState property changing
            if (e.PropertyName != "AuthState")
                return;

            // Abort if the user has not been authenticated
            if (AppContextViewModel.Instance.AuthState == AppContextViewModel.AuthStates.NotAuthenticated)
                return;

            // Stop handling events
            AppContextViewModel.Instance.PropertyChanged -= AppContextViewModel_PropertyChanged;

            // Close the login window and show the main window
            Application.Current.Dispatcher.Invoke(() =>
            {
                ShowMainWindow("MainWindow");
                _loginWindow.Close();
            });
        }

        #endregion


        #region Overrides

        public override void RunStartup()
        {
            // Replace the default ChangeFactory with our own one that can track data object modifications, and allows us to turn off change tracking
            DefaultChangeFactory.Current = new ChangeFactoryEx();

            base.RunStartup();
        }

        protected override void EnqueueStartupWorkers(StartupWorkerManager startupWorkerManager)
        {
            base.EnqueueStartupWorkers(startupWorkerManager);

            startupWorkerManager.Enqueue(new InitModulesStartupWorker());

            // If the AuthMethod is not Manual, add a startup worker to perform the login
            if (_commandLineOptions.Auth != CommandLineOptions.AuthMethods.Manual)
            {
                var loginWorker = new LoginStartupWorker(_commandLineOptions) { RethrowErrors = false };
                loginWorker.RunWorkerCompleted += LoginWorker_RunWorkerCompleted;
                startupWorkerManager.Enqueue(loginWorker);
            }
        }

        protected override void RunApplication()
        {
            base.RunApplication();

            // If the user has not been authenticated then show the login window, otherwise show the main window
            if (AppContextViewModel.Instance.AuthState == AppContextViewModel.AuthStates.NotAuthenticated)
                ShowLoginWindow();
            else
                ShowMainWindow("MainWindow");
        }

        protected override void OnMainWindowLoaded()
        {
            base.OnMainWindowLoaded();

            CloseSplashScreen();
        }

        #endregion
    }
}
