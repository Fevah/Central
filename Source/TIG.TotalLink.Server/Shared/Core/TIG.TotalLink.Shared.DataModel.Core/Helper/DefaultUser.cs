using System;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public class DefaultUser
    {
        #region Constructors

        /// <summary>
        /// Creates a default user with a UserName and Password.
        /// The Oid will be generated.
        /// </summary>
        /// <param name="userName">The name of the user.</param>
        /// <param name="displayName">The display name of the user.</param>
        /// <param name="encryptedPassword">The password of the user in encrypted form.</param>
        public DefaultUser(string userName, string displayName, string encryptedPassword)
        {
            UserName = userName;
            DisplayName = displayName;
            EncryptedPassword = encryptedPassword;
        }

        /// <summary>
        /// Creates a default user with an Oid and UserName.
        /// This user will not have any Password and should only be used as a service account.
        /// </summary>
        /// <param name="oid">The oid of the user.</param>
        /// <param name="userName">The name of the user.</param>
        /// <param name="displayName">The display name of the user.</param>
        public DefaultUser(Guid oid, string userName, string displayName)
        {
            Oid = oid;
            UserName = userName;
            DisplayName = displayName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The Oid of the user.
        /// </summary>
        public Guid Oid { get; private set; }

        /// <summary>
        /// The name of the user.
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// The display name of the user.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// The password of the user in encrypted form.
        /// </summary>
        public string EncryptedPassword { get; private set; }

        /// <summary>
        /// Indicates if this DefaultUser represents a service account.
        /// </summary>
        public bool IsServiceAccount
        {
            get { return !Equals(Oid, Guid.Empty); }
        }

        #endregion
    }
}
