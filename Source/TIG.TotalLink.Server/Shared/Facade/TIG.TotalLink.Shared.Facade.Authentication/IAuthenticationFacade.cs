using System;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Shared.Facade.Authentication
{
    public interface IAuthenticationFacade : IFacadeBase
    {
        /// <summary>
        /// Authenticate from token.
        /// </summary>
        /// <param name="token">The token to authenticate with.</param>
        /// <returns>True if the token could be authenticated; otherwise false.</returns>
        bool Authenticate(string token);

        /// <summary>
        /// Login method for system start.
        /// </summary>
        /// <param name="userName">User name for login.</param>
        /// <param name="password">Password for login.</param>
        /// <returns>Logged in user token when user was verified, otherwise returns an empty string.</returns>
        string Login(string userName, string password);

        /// <summary>
        /// Login by active directory method for verify current logged windows user.
        /// </summary>
        /// <param name="userSid">userSid for active directory user security identifier.</param>
        /// <param name="userGuid">GUID assocaited with current windows user.</param>
        /// <returns>Token for current logged in user.</returns>
        string LoginByActiveDirectory(byte[] userSid, Guid userGuid);

    }
}