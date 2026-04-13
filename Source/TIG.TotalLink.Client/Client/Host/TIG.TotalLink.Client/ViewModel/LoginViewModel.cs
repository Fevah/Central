using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.DirectoryServices.AccountManagement;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;
using DevExpress.Mvvm;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Command;
using TIG.TotalLink.Client.Helper;
using TIG.TotalLink.Shared.Facade.Core.Configuration;
using TIG.TotalLink.Shared.Facade.Core.Helper;

namespace TIG.TotalLink.Client.ViewModel
{
    public class LoginViewModel : ViewModelBase, IDataErrorInfo
    {
        #region Public Enums

        public enum AuthMethods
        {
            Windows = 1,
            TotalLink = 2,
            Offline = 3
        }

        #endregion


        #region Private Fields

        private readonly IServiceConfiguration _serviceConfiguration;
        private string _server;
        private int _basePort;
        private string _loginStatusMessage;
        private string _loginErrorMessage;
        private bool _isLoggingIn;
        private string _user;
        private SecureString _securePassword;
        private AuthMethods _authMethod = AuthMethods.TotalLink;

        #endregion


        #region Constructors

        public LoginViewModel()
        {
            // Initialize commands
            LoginCommand = new AsyncCommandEx(OnLoginExecuteAsync);
        }

        public LoginViewModel(IServiceConfiguration serviceConfiguration)
            : this()
        {
            // Store services
            _serviceConfiguration = serviceConfiguration;

            // Collect config
            Server = _serviceConfiguration.Server;
            BasePort = _serviceConfiguration.BasePort;
        }

        #endregion


        #region Commands

        /// <summary>
        /// Command to login.
        /// </summary>
        public ICommand LoginCommand { get; private set; }

        #endregion


        #region Public Properties

        /// <summary>
        /// The authentication method to use.
        /// </summary>
        public AuthMethods AuthMethod
        {
            get { return _authMethod; }
            set
            {
                SetProperty(ref _authMethod, value, () => AuthMethod, () =>
                {
                    RaisePropertyChanged(() => Server);
                    RaisePropertyChanged(() => BasePort);
                    RaisePropertyChanged(() => User);
                });
            }
        }

        /// <summary>
        /// Name of the server to connect to.
        /// </summary>
        [CustomValidation(typeof(LoginViewModel), "ValidateServer")]
        public string Server
        {
            get { return _server; }
            set { SetProperty(ref _server, value, () => Server); }
        }

        /// <summary>
        /// Base port number to use when connecting to services.
        /// </summary>
        [CustomValidation(typeof(LoginViewModel), "ValidateBasePort")]
        public int BasePort
        {
            get { return _basePort; }
            set { SetProperty(ref _basePort, value, () => BasePort); }
        }

        /// <summary>
        /// Login user.
        /// </summary>
        [CustomValidation(typeof(LoginViewModel), "ValidateUser")]
        public string User
        {
            get { return _user; }
            set { SetProperty(ref _user, value, () => User); }
        }

        /// <summary>
        /// A secure copy of the password that has been entered.
        /// </summary>
        public SecureString SecurePassword
        {
            get { return _securePassword; }
            set { SetProperty(ref _securePassword, value, () => SecurePassword); }
        }

        /// <summary>
        /// Describes the status of the login process.
        /// </summary>
        public string LoginStatusMessage
        {
            get { return _loginStatusMessage; }
            set { SetProperty(ref _loginStatusMessage, value, () => LoginStatusMessage); }
        }

        /// <summary>
        /// The current error message for any errors which occurred during an attempted login.
        /// </summary>
        public string LoginErrorMessage
        {
            get { return _loginErrorMessage; }
            set
            {
                SetProperty(ref _loginErrorMessage, value, () => LoginErrorMessage, () =>
                    RaisePropertyChanged(() => HasLoginError)
                );
            }
        }

        /// <summary>
        /// Indicates if there is a current login error message to display.
        /// </summary>
        public bool HasLoginError
        {
            get { return !string.IsNullOrWhiteSpace(LoginErrorMessage); }
        }

        /// <summary>
        /// Indicates if the login is in progress.
        /// </summary>
        public bool IsLoggingIn
        {
            get { return _isLoggingIn; }
            set { SetProperty(ref _isLoggingIn, value, () => IsLoggingIn); }
        }

        #endregion


        #region Validation Methods

        /// <summary>
        /// Validates the Server property.
        /// </summary>
        /// <param name="value">The value of the property that triggered validation.</param>
        /// <param name="validationContext">Contains info about the validation context.</param>
        /// <returns>A ValidationResult.</returns>
        public static ValidationResult ValidateServer(object value, ValidationContext validationContext)
        {
            // Attempt to get the LoginViewModel from the ValidationContext
            var loginViewModel = validationContext.ObjectInstance as LoginViewModel;
            if (loginViewModel == null)
                return ValidationResult.Success;

            // Don't validate if AuthMethod = Offline
            if (loginViewModel.AuthMethod == AuthMethods.Offline)
                return ValidationResult.Success;

            // Make sure the Server is not empty
            if (String.IsNullOrWhiteSpace(loginViewModel.Server))
                return new ValidationResult("Server is required.", new[] { "Server" });

            // If no errors were found, return success
            return ValidationResult.Success;
        }

        /// <summary>
        /// Validates the BasePort property.
        /// </summary>
        /// <param name="value">The value of the property that triggered validation.</param>
        /// <param name="validationContext">Contains info about the validation context.</param>
        /// <returns>A ValidationResult.</returns>
        public static ValidationResult ValidateBasePort(object value, ValidationContext validationContext)
        {
            // Attempt to get the LoginViewModel from the ValidationContext
            var loginViewModel = validationContext.ObjectInstance as LoginViewModel;
            if (loginViewModel == null)
                return ValidationResult.Success;

            // Don't validate if AuthMethod = Offline
            if (loginViewModel.AuthMethod == AuthMethods.Offline)
                return ValidationResult.Success;

            // Make sure the BasePort is within the range 1024 to 49151
            if (loginViewModel.BasePort < 1024 || loginViewModel.BasePort > 49151)
                return new ValidationResult("Port must be within the range 1024 to 49151.", new[] { "BasePort" });

            // If no errors were found, return success
            return ValidationResult.Success;
        }

        /// <summary>
        /// Validates the User property.
        /// </summary>
        /// <param name="value">The value of the property that triggered validation.</param>
        /// <param name="validationContext">Contains info about the validation context.</param>
        /// <returns>A ValidationResult.</returns>
        public static ValidationResult ValidateUser(object value, ValidationContext validationContext)
        {
            // Attempt to get the LoginViewModel from the ValidationContext
            var loginViewModel = validationContext.ObjectInstance as LoginViewModel;
            if (loginViewModel == null)
                return ValidationResult.Success;

            // Don't validate if AuthMethod != TotalLink
            if (loginViewModel.AuthMethod != AuthMethods.TotalLink)
                return ValidationResult.Success;

            // Make sure the User is not empty
            if (String.IsNullOrWhiteSpace(loginViewModel.User))
                return new ValidationResult("User is required.", new[] { "User" });

            // If no errors were found, return success
            return ValidationResult.Success;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Execute method for the LoginCommand.
        /// </summary>
        private async Task OnLoginExecuteAsync()
        {
            // Set the login status
            LoginErrorMessage = null;
            IsLoggingIn = true;

            // Update service cofiguration with values from the login window
            _serviceConfiguration.Server = Server;
            _serviceConfiguration.BasePort = BasePort;

            await Task.Run(() =>
            {
                try
                {
                    // Attempt to login using the selected AuthMethod
                    switch (AuthMethod)
                    {
                        case AuthMethods.Windows:
                            var userPrincipal = UserPrincipal.Current;

                            if (!userPrincipal.Guid.HasValue)
                            {
                                LoginErrorMessage = "Current user is not an Active Directory user!";
                                return;
                            }

                            LoginStatusMessage = "Logging in with Windows authentication...";
                            LoginHelper.DoWindowsLogin();
                            break;

                        case AuthMethods.TotalLink:
                            LoginStatusMessage = "Logging in with TotalLink authentication...";
                            LoginHelper.DoTotalLinkLogin(User, SecurePassword);
                            break;

                        case AuthMethods.Offline:
                            LoginStatusMessage = "Preparing Offline mode...";
                            LoginHelper.DoOfflineLogin();
                            break;
                    }

                    // Save ServiceConfiguration after a successful login
                    if (AppContextViewModel.Instance.AuthState != AppContextViewModel.AuthStates.NotAuthenticated)
                        _serviceConfiguration.Save();
                }
                catch (Exception ex)
                {
                    // If an error occurs, display it on the login window
                    var serviceException = new ServiceExceptionHelper(ex);
                    LoginErrorMessage = serviceException.Message;
                }
            });

            // Clear the login status
            IsLoggingIn = false;
            LoginStatusMessage = null;
        }

        #endregion


        #region IDataErrorInfo

        public string Error
        {
            get { return String.Empty; }
        }

        public string this[string columnName]
        {
            get { return IDataErrorInfoHelper.GetErrorText(this, columnName); }
        }

        #endregion
    }
}