using System;
using System.Linq;
using System.ServiceModel;
using System.Web.Security;
using DevExpress.Xpo;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Shared.Contract.Authentication;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.ActiveDirectory.Provider;
using TIG.TotalLink.Shared.DataModel.Admin;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.Facade.Admin;
using TIG.TotalLink.Shared.Facade.Core;
using ActiveDirectoryUser = TIG.TotalLink.Shared.DataModel.ActiveDirectory.ActiveDirectoryUser;

namespace TIG.TotalLink.Server.MethodService.Authentication
{
    public class AuthenticationMethodService : MethodServiceBase, IAuthenticationMethodService
    {
        #region Private Properties

        private static IAdminFacade _adminFacade;

        #endregion


        #region Public Methods

        /// <summary>
        /// Authenticate from token.
        /// </summary>
        /// <param name="token">The token to authenticate with.</param>
        /// <returns>True if the token could be authenticated; otherwise false.</returns>
        public bool Authenticate(string token)
        {
            // Fail if the token is empty
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // Attempt to extract the user data from the token
            UserData userData;
            try
            {
                var ticket = FormsAuthentication.Decrypt(token);
                if (ticket == null)
                    return false;

                userData = new UserData(ticket.UserData);
            }
            catch (Exception)
            {
                return false;
            }

            // Success if the guid is a service user oid
            if (DefaultUserCache.IsServiceUser(userData.Oid))
                return true;

            // Use the admin facade to look up the user
            var adminFacade = GetAdminDataFacade();
            if (adminFacade.ExecuteQuery(uow => uow.Query<User>().Where(p => p.Oid != userData.Oid)).Any())
                return true;

            // Fail if we didn't find the user anywhere
            throw new FaultException<ServiceFault>(new ServiceFault("Illegal authentication token."));
        }

        /// <summary>
        /// Login method for system start.
        /// </summary>
        /// <param name="userName">User name for login.</param>
        /// <param name="password">Password for login.</param>
        /// <returns>Logged in user token when user was verified, otherwise returns an empty string.</returns>
        public string Login(string userName, string password)
        {
            var adminFacade = GetAdminDataFacade();

            // Every system will make sure login name unique
            // So we just use first or defaut to check current login user
            var user = adminFacade.ExecuteQuery(uow => uow.Query<User>().Where(p => p.UserName == userName)).FirstOrDefault();
            if (user == null)
            {
                var errorMessage = "Username or password is incorrect!";
#if DEBUG
                errorMessage += "\r\n(DEBUG: Failed to find TotalLink user.)";
#endif
                throw new FaultException<ServiceFault>(new ServiceFault(errorMessage));
            }

            // Error if the password is empty
            if (string.IsNullOrWhiteSpace(password))
            {
                var errorMessage = "Username or password is incorrect!";
#if DEBUG
                errorMessage += "\r\n(DEBUG: Password was empty.)";
#endif
                throw new FaultException<ServiceFault>(new ServiceFault(errorMessage));
            }

            // Use user inputed password to generate password hash 
            var passwordEncrytion = PasswordHasher.GeneratePasswordHash(password, user.Salt);

            // Compare real password hash to verify user inputed password
            if (user.Password != passwordEncrytion)
            {
                var errorMessage = "Username or password is incorrect!";
#if DEBUG
                errorMessage += "\r\n(DEBUG: Password did not match.)";
#endif
                throw new FaultException<ServiceFault>(new ServiceFault(errorMessage));
            }

            // Made a ticket by user id
            var ticket = new FormsAuthenticationTicket(1,
                userName,
                DateTime.Now,
                // TODO: [Bo] This part logic we need to save it to setting file in future
                // And we need to think about token expiration thing in next phase
                DateTime.Now.AddDays(10),
                true,
                new UserData(user.Oid, user.DisplayName).ToString());

            return FormsAuthentication.Encrypt(ticket);
        }

        /// <summary>
        /// Login by active directory method for verify current logged windows user.
        /// </summary>
        /// <param name="userSid">userSid for active directory user security identifier.</param>
        /// <param name="userGuid">GUID assocaited with current windows user.</param>
        /// <returns>Token for current logged in user.</returns>
        public string LoginByActiveDirectory(byte[] userSid, Guid userGuid)
        {
            // Verify user information in active directory.
            var activeDirectoryContextProvider = new ActiveDirectoryContextProvider();
            var activeDirectoryUser = activeDirectoryContextProvider.Context.Query<ActiveDirectoryUser>().FirstOrDefault(item => item.Oid == userGuid && item.Sid == userSid);

            // If AD user not found or user is inactive then access is denied
            if (activeDirectoryUser == null
                || !activeDirectoryUser.IsActive)
            {
                var errorMessage = "Windows login failed!";
#if DEBUG
                errorMessage += "\r\n(DEBUG: Failed to find Active Directory user.)";
#endif
                throw new FaultException<ServiceFault>(new ServiceFault(errorMessage));
            }

            // Verify user in totallink system.
            var storedUser = GetUserByActiveDirectoryId(userGuid);
            if (storedUser == null)
            {
                var errorMessage = "Windows login failed!";
#if DEBUG
                errorMessage += "\r\n(DEBUG: Failed to find TotalLink user.)";
#endif
                throw new FaultException<ServiceFault>(new ServiceFault(errorMessage));
            }

            // Made a ticket by user id
            var ticket = new FormsAuthenticationTicket(1,
                storedUser.UserName,
                DateTime.Now,
                // TODO: [Bo] This part logic we need to save it to setting file in future
                // And we need to think about token expiration thing in next phase
                DateTime.Now.AddDays(10),
                true,
                new UserData(storedUser.Oid, storedUser.DisplayName).ToString());

            return FormsAuthentication.Encrypt(ticket);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes and returns an AdminFacade connected to the data service only.
        /// </summary>
        /// <returns>An AdminFacade.</returns>
        private static IAdminFacade GetAdminDataFacade()
        {
            if (_adminFacade == null)
                _adminFacade = new AdminFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_adminFacade != null && !_adminFacade.IsDataConnected)
                    _adminFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Admin Facade!", ex));
            }

            return _adminFacade;
        }

        /// <summary>
        /// Returns a user by matching the ActiveDirectoryId.
        /// </summary>
        /// <param name="activeDirectoryId">The ActiveDirectoryId of the user to find.</param>
        /// <returns>The User with a matching ActiveDirectoryId, or null if none was found.</returns>
        private User GetUserByActiveDirectoryId(Guid activeDirectoryId)
        {
            var adminFacade = GetAdminDataFacade();
            return adminFacade.ExecuteQuery(uow => uow.Query<User>().Where(item => item.ActiveDirectoryId == activeDirectoryId)).FirstOrDefault();
        }

        #endregion
    }
}
