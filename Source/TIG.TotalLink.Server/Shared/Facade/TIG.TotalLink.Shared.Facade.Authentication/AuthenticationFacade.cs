using System;
using TIG.TotalLink.Shared.Contract.Authentication;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Authentication
{
    [Facade("Authentication", 1)]
    public class AuthenticationFacade : FacadeBase<DataObjectBase, IAuthenticationMethodService>, IAuthenticationFacade
    {
        #region Constructors

        public AuthenticationFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Authenticate from token.
        /// </summary>
        /// <param name="token">The token to authenticate with.</param>
        /// <returns>True if the token could be authenticated; otherwise false.</returns>
        public bool Authenticate(string token)
        {
            return MethodFacade.Execute(m => m.Authenticate(token));
        }

        /// <summary>
        /// Login method for system start.
        /// </summary>
        /// <param name="userName">User name for login.</param>
        /// <param name="password">Password for login.</param>
        /// <returns>Logged in user token when user was verified, otherwise returns an empty string.</returns>
        public string Login(string userName, string password)
        {
            return MethodFacade.Execute(m => m.Login(userName, password));
        }

        /// <summary>
        /// Login by active directory method for verify current logged windows user.
        /// </summary>
        /// <param name="userSid">userSid for active directory user security identifier.</param>
        /// <param name="userGuid">GUID assocaited with current windows user.</param>
        /// <returns>Token for current logged in user.</returns>
        public string LoginByActiveDirectory(byte[] userSid, Guid userGuid)
        {
            return MethodFacade.Execute(m => m.LoginByActiveDirectory(userSid, userGuid));
        }

        #endregion
    }
}