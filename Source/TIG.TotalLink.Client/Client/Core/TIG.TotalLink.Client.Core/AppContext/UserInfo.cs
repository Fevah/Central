using System;
using System.Web.Security;
using DevExpress.Mvvm;
using TIG.TotalLink.Shared.DataModel.Core.Helper;

namespace TIG.TotalLink.Client.Core.AppContext
{
    public class UserInfo : BindableBase
    {
        #region Private Fields

        private Guid _oid;
        private string _userName;
        private string _displayName;
        private string _token;

        #endregion


        #region Constructors

        public UserInfo(string token)
        {
            Token = token;

            // Decrypt the token to a ticket
            var ticket = FormsAuthentication.Decrypt(token);
            if (ticket == null)
                return;

            // Initialize with data from the ticket
            var userData = new UserData(ticket.UserData);
            UserName = ticket.Name;
            Oid = userData.Oid;
            DisplayName = userData.DisplayName;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// The Oid of the user.
        /// </summary>
        public Guid Oid
        {
            get { return _oid; }
            private set { SetProperty(ref _oid, value, () => Oid); }
        }

        /// <summary>
        /// The name of the user.
        /// </summary>
        public string UserName
        {
            get { return _userName; }
            private set { SetProperty(ref _userName, value, () => UserName); }
        }

        /// <summary>
        /// The display name for the user.
        /// </summary>
        public string DisplayName
        {
            get { return _displayName; }
            private set { SetProperty(ref _displayName, value, () => DisplayName); }
        }

        /// <summary>
        /// The authentication token that was used to construct this UserInfo.
        /// </summary>
        public string Token
        {
            get { return _token; }
            private set { SetProperty(ref _token, value, () => Token); }
        }

        #endregion
    }
}
