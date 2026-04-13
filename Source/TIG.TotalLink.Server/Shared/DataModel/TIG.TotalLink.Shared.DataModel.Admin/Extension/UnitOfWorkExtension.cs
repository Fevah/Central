using System.Collections.Generic;
using System.Web.Security;
using DevExpress.Xpo;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Shared.DataModel.Admin.Extension
{
    public static class UnitOfWorkExtension
    {
        #region Private Fields

        private static readonly Dictionary<string, User> ImpersonatedUsers = new Dictionary<string, User>();

        #endregion


        #region Public Methods

        /// <summary>
        /// Begins impersonating a user for CreatedBy and ModifiedBy stamping.
        /// Impersonation will continue until the session is disposed.
        /// </summary>
        /// <param name="uow">The UnitOfWork to begin impersonation on.</param>
        /// <param name="token">An encrypted authentication token containing the user to impersonate.</param>
        public static void ImpersonateUser(this UnitOfWork uow, string token)
        {
            // Abort if no token was supplied
            if (token == null)
                return;

            try
            {
                // Decrypt the token to a ticket
                var ticket = FormsAuthentication.Decrypt(token);
                if (ticket == null)
                    return;

                // Extract userdata from the ticket
                var userData = new UserData(ticket.UserData);

                // Start impersonating the user stored in the token
                var sessionKey = uow.ToString();
                var user = uow.GetObjectByKey<User>(userData.Oid);
                if (ImpersonatedUsers.ContainsKey(sessionKey))
                {
                    ImpersonatedUsers[sessionKey] = user;
                }
                else
                {
                    ImpersonatedUsers.Add(sessionKey, user);
                    uow.Disposed += UnitOfWork_Disposed;
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Ends impersonation of a user for CreatedBy and ModifiedBy stamping.
        /// </summary>
        /// <param name="uow">The UnitOfWork to end impersonation on.</param>
        public static void StopImpersonatingUser(this UnitOfWork uow)
        {
            // Abort if the session is not currently impersonating
            var sessionKey = uow.ToString();
            if (!ImpersonatedUsers.ContainsKey(sessionKey))
                return;

            // Remove the impersonation
            ImpersonatedUsers.Remove(sessionKey);
            uow.Disposed -= UnitOfWork_Disposed;
        }

        /// <summary>
        /// Gets the user who is currently being impersonated on this UnitOfWork.
        /// </summary>
        /// <param name="uow">The UnitOfWork to get the impersonated user for.</param>
        /// <returns>The user currently being impersonated on the UnitOfWork, or null if no user is being impersonated.</returns>
        public static User GetImpersonatedUser(this UnitOfWork uow)
        {
            User user;
            ImpersonatedUsers.TryGetValue(uow.ToString(), out user);
            return user;
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Handles the UnitOfWork.Disposed event.
        /// </summary>
        /// <param name="sender">The object which raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private static void UnitOfWork_Disposed(object sender, System.EventArgs e)
        {
            // Stop impersonation when the UnitOfWork is disposed
            StopImpersonatingUser((UnitOfWork)sender);
        }

        #endregion
    }
}
