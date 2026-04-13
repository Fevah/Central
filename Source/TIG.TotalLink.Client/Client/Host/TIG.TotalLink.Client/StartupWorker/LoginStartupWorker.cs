using System;
using System.ComponentModel;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.StartupWorker.Core;
using TIG.TotalLink.Client.Helper;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Client.StartupWorker
{
    /// <summary>
    /// Performs the login when login info was passed on the command line.
    /// </summary>
    public class LoginStartupWorker : StartupWorkerBase
    {
        #region Private Fields

        private readonly CommandLineOptions _commandLineOptions;

        #endregion


        #region Constructors

        public LoginStartupWorker()
        {
        }

        public LoginStartupWorker(CommandLineOptions commandLineOptions)
            : this()
        {
            _commandLineOptions = commandLineOptions;
        }

        #endregion


        #region Overrides

        public override void Initialize()
        {
            // Record the number of steps it will take to do the work
            Steps = 1;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            base.OnDoWork(e);

            // Use the view locator to find the the login service configuration
            var serviceConfiguration = (AutofacViewLocator.Default.Resolve<IServiceConfiguration>());
            if (serviceConfiguration == null)
                throw new Exception("Failed to create service configuration!");

            // Update parameters in the service configuration from the command line options
            if (!string.IsNullOrWhiteSpace(_commandLineOptions.Server))
                serviceConfiguration.Server = _commandLineOptions.Server;
            if (_commandLineOptions.BasePort.HasValue)
                serviceConfiguration.BasePort = _commandLineOptions.BasePort.Value;

            // Attempt to login using the specified method
            switch (_commandLineOptions.Auth)
            {
                case CommandLineOptions.AuthMethods.Unspecified:
                case CommandLineOptions.AuthMethods.Windows:
                    ReportProgress(0, "Logging in with Windows authentication...");
                    LoginHelper.DoWindowsLogin();
                    break;

                case CommandLineOptions.AuthMethods.TotalLink:
                    ReportProgress(0, "Logging in with TotalLink authentication...");
                    LoginHelper.DoTotalLinkLogin(_commandLineOptions.Username, _commandLineOptions.SecurePassword);
                    break;

                case CommandLineOptions.AuthMethods.Offline:
                    ReportProgress(0, "Preparing Offline mode...");
                    LoginHelper.DoOfflineLogin();
                    break;
            }

            // Save ServiceConfiguration after a successful login
            if (AppContextViewModel.Instance.AuthState != AppContextViewModel.AuthStates.NotAuthenticated)
                serviceConfiguration.Save();
        }

        #endregion
    }
}
