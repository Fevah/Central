using System.Configuration;
using System.Web.Configuration;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Server.Core.Configuration
{
    public class ServerServiceConfiguration : IServiceConfiguration
    {
        private readonly string _authenticationToken;

        #region Private Fields

        private string _server = "localhost";
        private int _basePort = 42000;

        #endregion


        #region Constructors

        public ServerServiceConfiguration()
        {
            Load();
        }

        public ServerServiceConfiguration(string authenticationToken)
            : this()
        {
            _authenticationToken = authenticationToken;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Authentication token to use when accessing services.
        /// </summary>
        public string AuthenticationToken
        {
            get { return _authenticationToken; }
        }

        /// <summary>
        /// Name of the server to connect to.
        /// </summary>
        public string Server
        {
            get { return _server; }
            set { _server = value; }
        }

        /// <summary>
        /// Base port number to use when connecting to services.
        /// The Global Data Service should be on this port, and all other services on offsets from this port.
        /// </summary>
        public int BasePort
        {
            get { return _basePort; }
            set { _basePort = value; }
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Load the configuration.
        /// </summary>
        public void Load()
        {
            // Open the config file
            var config = WebConfigurationManager.OpenWebConfiguration("/");

            // Read the Server setting
            var serverSetting = config.AppSettings.Settings["Server"];
            if (serverSetting != null)
                Server = serverSetting.Value;

            // Read the BasePort setting
            var basePortSetting = config.AppSettings.Settings["BasePort"];
            if (basePortSetting != null)
            {
                int basePort;
                if (int.TryParse(basePortSetting.Value, out basePort))
                    BasePort = basePort;
            }
        }

        /// <summary>
        /// Save the configuration.
        /// </summary>
        public void Save()
        {
            // Open the config file
            var config = WebConfigurationManager.OpenWebConfiguration("/");

            // Write the Server setting
            var serverSetting = config.AppSettings.Settings["Server"];
            if (serverSetting != null)
                serverSetting.Value = Server;
            else
                config.AppSettings.Settings.Add("Server", Server);

            // Write the BasePort setting
            var basePortSetting = config.AppSettings.Settings["BasePort"];
            if (basePortSetting != null)
                basePortSetting.Value = BasePort.ToString();
            else
                config.AppSettings.Settings.Add("BasePort", BasePort.ToString());

            // Save the config file
            config.Save(ConfigurationSaveMode.Modified);
        }

        #endregion
    }
}
