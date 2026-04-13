namespace TIG.TotalLink.Shared.Facade.Core.Configuration
{
    public interface IServiceConfiguration
    {
        #region Public Properties

        /// <summary>
        /// Name of the server to connect to.
        /// </summary>
        string Server { get; set; }

        /// <summary>
        /// Base port number to use when connecting to services.
        /// The Global Data Service should be on this port, and all other services on offsets from this port.
        /// </summary>
        int BasePort { get; set; }

        /// <summary>
        /// Authentication token
        /// </summary>
        string AuthenticationToken { get; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Load the configuration.
        /// </summary>
        void Load();

        /// <summary>
        /// Save the configuration.
        /// </summary>
        void Save();

        #endregion
    }
}
