using System;
using System.Security;
using CommandLine;
using TIG.TotalLink.Client.Core.Extension;

namespace TIG.TotalLink.Client
{
    /// <summary>
    /// Command Line Options for login or setting system in background.
    /// </summary>
    public class CommandLineOptions
    {
        #region Enums

        /// <summary>
        /// Methods for logging into the system.
        /// </summary>
        public enum AuthMethods
        {
            Unspecified = 0,    // No auth method was specified.  Will attempt to login via Window authentication.  On failure, the application will start in offline mode.
            Windows = 1,        // Will attempt to login via Window authentication.  On failure, the login window will be displayed.
            TotalLink = 2,      // Will attempt to login via TotalLink authentication using the specified username and password.  On failure, the login window will be displayed.
            Offline = 3,        // Login will be skipped and application will start in offline mode.
            Manual = 4          // Login window will be displayed immediately.  No automatic login will be attempted.
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// A link to open.
        /// </summary>
        [Option('h', "hyperlink", DefaultValue = null, HelpText = "A link to open.")]
        public string Hyperlink { get; set; }

        /// <summary>
        /// The auth method to use.
        /// </summary>
        [Option('a', "auth", DefaultValue = AuthMethods.Unspecified, HelpText = "Specify an alternative method of authentication.")]
        public AuthMethods Auth { get; set; }

        /// <summary>
        /// Name of server to connect to.
        /// </summary>
        [Option('s', "server", DefaultValue = null, HelpText = "Name of server to connect to.")]
        public string Server { get; set; }

        /// <summary>
        /// The base port for connecting to services.
        /// </summary>
        [Option('b', "base-port", DefaultValue = null, HelpText = "The base port for connecting to services.")]
        public int? BasePort { get; set; }

        /// <summary>
        /// Username to use when using TotalLink authentication method.
        /// </summary>
        [Option('u', "username", DefaultValue = null, HelpText = "Username to use when using TotalLink authentication method.")]
        public string Username { get; set; }

        /// <summary>
        /// Password to use when using TotalLink authentication method.  Supplying the password using this method is insecure and should only be used for debug purposes.
        /// </summary>
        [Option('p', "password", DefaultValue = null, HelpText = "Password to use when using TotalLink authentication method.  Supplying the password using this method is insecure and should only be used for debug purposes.")]
        public string Password
        {
            get { throw new NotImplementedException(); }
            set
            {
                SecurePassword = (value == null ? null : value.ToSecureString());
            }
        }

        /// <summary>
        /// Delays each step in the startup workers by the specifed number of milliseconds.
        /// </summary>
        [Option('d', "delay-startup", DefaultValue = 0, HelpText = "Delays each step in the startup workers by the specifed number of milliseconds.")]
        public int DelayStartup { get; set; }

        /// <summary>
        /// SecurePassword use for encrypt password.
        /// </summary>
        public SecureString SecurePassword { get; private set; }

        #endregion
    }
}
