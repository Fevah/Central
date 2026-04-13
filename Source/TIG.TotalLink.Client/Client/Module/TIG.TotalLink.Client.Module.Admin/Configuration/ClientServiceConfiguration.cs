using TIG.TotalLink.Client.Core.AppContext;
using TIG.TotalLink.Client.Module.Admin.Properties;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Client.Module.Admin.Configuration
{
    public class ClientServiceConfiguration : IServiceConfiguration
    {
        #region Constructors

        public ClientServiceConfiguration()
        {
            UpgradeSettings();
            Load();
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Authentication token to use when accessing services.
        /// </summary>
        public string AuthenticationToken
        {
            get { return (AppContextViewModel.Instance.UserInfo != null ? AppContextViewModel.Instance.UserInfo.Token : null); }
        }
       
        /// <summary>
        /// Name of the server to connect to.
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Base port number to use when connecting to services.
        /// The Global Data Service should be on this port, and all other services on offsets from this port.
        /// </summary>
        public int BasePort { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Load the configuration.
        /// </summary>
        public void Load()
        {
            Server = Settings.Default.Server;
            BasePort = Settings.Default.BasePort;
        }

        /// <summary>
        /// Save the configuration.
        /// </summary>
        public void Save()
        {
            Settings.Default.Server = Server;
            Settings.Default.BasePort = BasePort;
            Settings.Default.Save();
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Migrates settings from a previous version of the application if required.
        /// </summary>
        private void UpgradeSettings()
        {
            // Abort if no upgrade is required
            if (!Settings.Default.CallUpgrade)
                return;

            // Migrate settings
            Settings.Default.Upgrade();
            Settings.Default.CallUpgrade = false;
            Settings.Default.Save();
        }

        #endregion
    }
}
