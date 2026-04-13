using System;
using System.DirectoryServices.AccountManagement;
using System.Security;
using System.Security.Principal;
using TIG.TotalLink.Client.Core;
using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Core.Extension;
using TIG.TotalLink.Shared.Facade.Authentication;

namespace TIG.TotalLink.Client.Helper
{
    public class LoginHelper
    {
        #region Public Methods

        /// <summary>
        /// Attempts to log the user in using Windows authentication.
        /// </summary>
        public static void DoWindowsLogin()
        {
            try
            {
                // Get the current user
                var userPrincipal = UserPrincipal.Current;

                // Abort if the user is not an Active Directory user
                if (!userPrincipal.Guid.HasValue)
                    throw new Exception("Current user is not a valid Active Directory user!");

                // Attempt to get the AuthenticationFacade
                var authenticationFacade = AutofacViewLocator.Default.Resolve<IAuthenticationFacade>();
                if (authenticationFacade == null)
                    throw new Exception("Failed to connect to Authentication Facade!");

                // Connect to the AuthenticationFacade
                authenticationFacade.Connect();

                // Attempt to perform ActiveDirectory login
                var binarySid = new byte[userPrincipal.Sid.BinaryLength];
                userPrincipal.Sid.GetBinaryForm(binarySid, 0);
                var token = authenticationFacade.LoginByActiveDirectory(binarySid, userPrincipal.Guid.Value);

                // Abort if no token was returned
                if (string.IsNullOrEmpty(token))
                    throw new Exception("No authentication token was received!");

                // Set the login state
                AppContextViewModel.Instance.UserInfo = new UserInfo(token);
                AppContextViewModel.Instance.AuthState = AppContextViewModel.AuthStates.Windows;
            }
            catch (Exception)
            {
                AppContextViewModel.Instance.AuthState = AppContextViewModel.AuthStates.NotAuthenticated;
                throw;
            }
        }

        /// <summary>
        /// Attempts to log the user in using TotalLink authentication.
        /// </summary>
        /// <param name="username">User name use for login to system.</param>
        /// <param name="securePassword">A secure copy of the password that has been entered.</param>
        public static void DoTotalLinkLogin(string username, SecureString securePassword)
        {
            try
            {
                // Abort if the username or password is empty
                if (string.IsNullOrEmpty(username) || securePassword == null || string.IsNullOrEmpty(securePassword.ToPasswordHash()))
                    throw new Exception("Username and password cannot be blank!");

                // Attempt to get the AuthenticationFacade
                var authenticationFacade = AutofacViewLocator.Default.Resolve<IAuthenticationFacade>();
                if (authenticationFacade == null)
                    throw new Exception("Failed to connect to Authentication Facade!");

                // Connect to the AuthenticationFacade
                authenticationFacade.Connect();

                // Attempt to perform TotalLink login
                var token = authenticationFacade.Login(username, securePassword.ToPasswordHash());

                // Abort if no token was returned
                if (string.IsNullOrEmpty(token))
                    throw new Exception("No authentication token was received!");

                // Set the login state
                AppContextViewModel.Instance.UserInfo = new UserInfo(token);
                AppContextViewModel.Instance.AuthState = AppContextViewModel.AuthStates.TotalLink;
            }
            catch (Exception)
            {
                AppContextViewModel.Instance.AuthState = AppContextViewModel.AuthStates.NotAuthenticated;
                throw;
            }
        }

        /// <summary>
        /// Attempts to log the user in using Offline authentication.
        /// </summary>
        public static void DoOfflineLogin()
        {
            // Set the login state
            AppContextViewModel.Instance.UserInfo = null;
            AppContextViewModel.Instance.AuthState = AppContextViewModel.AuthStates.Offline;
        }

        #endregion
    }
}
