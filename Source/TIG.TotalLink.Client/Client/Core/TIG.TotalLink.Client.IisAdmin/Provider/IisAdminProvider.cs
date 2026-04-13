using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading;
using Microsoft.Web.Administration;

namespace TIG.TotalLink.Client.IisAdmin.Provider
{
    public class IisAdminProvider : IIisAdminProvider
    {
        #region Private Fields

        private ChannelFactory<IIisAdmin> _iisAdminChannelFactory;
        private IIisAdmin _iisAdminChannel;
        private bool _isConnected;

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets a list of IIS Sites.
        /// </summary>
        /// <returns>A list of IIS Sites</returns>
        public ObservableCollection<IisSite> GetSites()
        {
            if (!_isConnected)
                Connect();

            // Attempt to collect the site list twice in case the channel is currently faulted
            ObservableCollection<IisSite> sites = null;
            var retryCount = 2;
            while (retryCount > 0)
            {
                // Decrement the retry count
                retryCount--;

                try
                {
                    // Attempt to collect the site list
                    sites = new ObservableCollection<IisSite>(_iisAdminChannel.GetSites());
                    retryCount = 0;
                }
                catch (Exception ex)
                {
                    // If this is the last retry, throw an error
                    if (retryCount == 0)
                        throw new Exception("Failed to load service list!", ex);

                    // If this is not the last retry, attempt to reconnect to the WCF service
                    ConnectToIisAdmin();
                }
            }

            return sites;
        }

        /// <summary>
        /// Configures an IIS Site.
        /// </summary>
        /// <param name="site">The IIS Site to configure.</param>
        /// <param name="isStart">Indicates whether the site should be started or not.</param>
        public void ConfigureSite(IisSite site, bool isStart)
        {
            // Attempt to configure the site twice in case the channel is currently faulted
            var retryCount = 2;
            while (retryCount > 0)
            {
                // Decrement the retry count
                retryCount--;

                try
                {
                    // Attempt to configure the site
                    var result = _iisAdminChannel.ConfigureSite(site.Id, isStart);
                    retryCount = 0;

                    // If the configure was not successful, throw an error
                    if (!result)
                        throw new Exception("Failed to configure service!");

                    // If the configure was successful, wait for the site to start or stop
                    var siteChanged = false;
                    var repeats = 10;
                    do
                    {
                        Thread.Sleep(1000);
                        var siteState = GetSites().FirstOrDefault(s => s.Name == site.Name);
                        if (siteState == null
                            || (isStart && siteState.State == ObjectState.Started)
                            || (!isStart && siteState.State == ObjectState.Stopped)
                            || siteState.State == ObjectState.Unknown
                        )
                            siteChanged = true;

                        repeats--;
                    } while (!siteChanged && repeats > 0);
                }
                catch (Exception ex)
                {
                    // If this is the last retry, throw an error
                    if (retryCount == 0)
                        throw new Exception("Failed to configure service!", ex);

                    // If this is not the last retry, attempt to reconnect to the WCF service
                    ConnectToIisAdmin();
                }
            }
        }

        /// <summary>
        /// Restarts an IIS Site.
        /// </summary>
        /// <param name="site">The IIS Site to restart.</param>
        public void RestartSite(IisSite site)
        {
            // Stop the site
            ConfigureSite(site, false);

            // Start the site
            ConfigureSite(site, true);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes the connection with the IIS Admin WCF service.
        /// </summary>
        private void Connect()
        {
            // Create a channel factory for connecting to the IIS Admin WCF service
            _iisAdminChannelFactory = new ChannelFactory<IIisAdmin>(new NetNamedPipeBinding(), new EndpointAddress("net.pipe://localhost/TIG.TotalLink.IisAdmin"));

            // Attempt to connect to the service
            _isConnected = ConnectToIisAdmin();
            if (!_isConnected)
            {
                // If the connection failed, start the IIS Admin application
                RunIisAdmin();

                // Try to connect 5 times with 2 second delays between each attempt
                var retryCount = 5;
                while (retryCount > 0 && !_isConnected)
                {
                    Thread.Sleep(2000);
                    _isConnected = ConnectToIisAdmin();
                    retryCount--;
                }
            }

            // If none of the connection attempts were sucessful, give up with an error
            if (!_isConnected)
                throw new Exception("Failed to connect to the IIS Admin application!");
        }

        /// <summary>
        /// Creates a channel to the IIS Admin WCF service and tries to ping it.
        /// </summary>
        /// <returns>True if the connection was successful; otherwise false.</returns>
        private bool ConnectToIisAdmin()
        {
            try
            {
                _iisAdminChannel = _iisAdminChannelFactory.CreateChannel();
                _iisAdminChannel.Ping();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Run the IIS Admin application with elevated permissions.
        /// </summary>
        private void RunIisAdmin()
        {
            try
            {
                var startInfo = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "TIG.TotalLink.IisAdmin.exe"))
                {
                    Verb = "runas",
                    Arguments = string.Format("--parent-process-id {0}{1}", Process.GetCurrentProcess().Id, (Debugger.IsAttached ? " --express" : null))
                };
                var process = new Process() { StartInfo = startInfo };
                process.Start();
            }
            catch
            {
                // Ignore errors
            }
        }

        #endregion
    }
}
