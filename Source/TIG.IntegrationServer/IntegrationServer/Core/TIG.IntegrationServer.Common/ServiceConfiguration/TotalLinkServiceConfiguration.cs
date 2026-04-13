using System.Configuration;
using System.Reflection;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.IntegrationServer.Common.ServiceConfiguration
{
    public class TotalLinkServiceConfiguration : IServiceConfiguration
    {
        #region Private Fields

        private string _server = "localhost";
        private int _basePort = 42000;
        private readonly string _authenticationToken;

        #endregion


        #region Constructors

        public TotalLinkServiceConfiguration()
        {
            Load();
        }

        public TotalLinkServiceConfiguration(string authenticationToken)
            : this()
        {
            _authenticationToken = authenticationToken;
        }

        #endregion


        #region Public Properties

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
            //Read entry assembly location
            var assemblyConfigFile = Assembly.GetEntryAssembly().Location;

            // Open the config file
            var config = ConfigurationManager.OpenExeConfiguration(assemblyConfigFile);

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
            //Read entry assembly location
            var assemblyConfigFile = Assembly.GetEntryAssembly().Location;

            // Open the config file
            var config = ConfigurationManager.OpenExeConfiguration(assemblyConfigFile);

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
