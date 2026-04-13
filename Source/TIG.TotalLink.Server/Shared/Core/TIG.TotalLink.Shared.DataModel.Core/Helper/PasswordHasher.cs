using System;
using System.Security.Cryptography;
using System.Text;

namespace TIG.TotalLink.Shared.DataModel.Core.Helper
{
    public static class PasswordHasher
    {
        /// <summary>
        /// Generate password hash
        /// </summary>
        /// <param name="password">password</param>
        /// <param name="salt">salt</param>
        /// <returns></returns>
        public static string GeneratePasswordHash(string password, string salt)
        {
            var passwordAndSaltBytes = Encoding.UTF8.GetBytes(password + salt);
            var hashBytes = new SHA256Managed().ComputeHash(passwordAndSaltBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
