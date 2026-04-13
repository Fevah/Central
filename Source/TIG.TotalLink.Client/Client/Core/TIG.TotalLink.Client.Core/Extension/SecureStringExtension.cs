using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace TIG.TotalLink.Client.Core.Extension
{
    /// <summary>
    /// Extension SecureString Class.
    /// </summary>
    public static class SecureStringExtension
    {
        /// <summary>
        /// UnsecureString from secured string.
        /// </summary>
        /// <param name="securePassword">Secured password.</param>
        /// <returns>String of unsecured.</returns>
        public static string ToUnsecureString(this SecureString securePassword)
        {
            if (securePassword == null)
                throw new ArgumentNullException("securePassword");

            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }

        /// <summary>
        /// Generate secure string from free text.
        /// </summary>
        /// <param name="password">Text you want to make it secure.</param>
        /// <returns>Secure string from text.</returns>
        public static SecureString ToSecureString(this string password)
        {
            if (password == null)
                throw new ArgumentNullException("password");

            var secureString = new SecureString();

            foreach (var c in password)
            {
                secureString.AppendChar(c);
            }

            secureString.MakeReadOnly();
            return secureString;
        }

        /// <summary>
        /// Convert secure string to hash value.
        /// </summary>
        /// <param name="password">Secure string for encryption.</param>
        /// <returns>Encrypted password.</returns>
        public static string ToPasswordHash(this SecureString password)
        {
            var passwordAndSaltBytes = System.Text.Encoding.UTF8.GetBytes(password.ToUnsecureString());
            var hashBytes = new SHA256Managed().ComputeHash(passwordAndSaltBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}