using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Security;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public class DefaultUserCache
    {
        #region Public Constants

        public const string ServiceToServiceUserName = "Service-To-Service";

        #endregion


        #region Private Fields

        private static readonly List<DefaultUser> _defaultUsers;

        #endregion


        #region Constructors

        static DefaultUserCache()
        {
            _defaultUsers = new List<DefaultUser>()
            {
                new DefaultUser(new Guid("6E85E3EE-89ED-4AC9-AFFB-1C9515675D55"), "system", "System"),
                new DefaultUser("admin", "Admin", "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg="),
                new DefaultUser(new Guid("00F0B4AD-C826-4938-9CC3-0CD7CE5889F3"), ServiceToServiceUserName, ServiceToServiceUserName)
            };
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A list of all default users.
        /// </summary>
        public static List<DefaultUser> DefaultUsers
        {
            get { return _defaultUsers; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Logs in a service user.
        /// Used when a service communicates to another service.
        /// </summary>
        /// <param name="username">The name of the service user to login as.</param>
        /// <returns>The authentication token for the specified user, or null if the user does not exist.</returns>
        public static string LoginServiceUser(string username)
        {
            var user = DefaultUsers.FirstOrDefault(u => u.IsServiceAccount && u.UserName == username);
            if (user == null)
                return null;

            // Generate a ticket so this service can talk to the Admin service
            var ticket = new FormsAuthenticationTicket(1,
                user.UserName,
                DateTime.Now,
                // TODO: [Bo] This part logic we need to save it to setting file in future
                // And we need to think about token expiration thing in next phase
                DateTime.Now.AddDays(10),
                true,
                new UserData(user.Oid, user.DisplayName).ToString());

            return FormsAuthentication.Encrypt(ticket);
        }

        /// <summary>
        /// Tests if the specified oid belongs to a service user.
        /// </summary>
        /// <param name="oid">The user oid to test.</param>
        /// <returns>True if the oid matches a service user; otherwise false.</returns>
        public static bool IsServiceUser(Guid oid)
        {
            return DefaultUsers.Any(u => u.Oid == oid);
        }

        #endregion
    }
}
