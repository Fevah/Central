using System.Collections.ObjectModel;

namespace TIG.TotalLink.Client.IisAdmin.Provider
{
    public interface IIisAdminProvider
    {
        /// <summary>
        /// Gets a list of IIS Sites.
        /// </summary>
        /// <returns>A list of IIS Sites</returns>
        ObservableCollection<IisSite> GetSites();

        /// <summary>
        /// Configures an IIS Site.
        /// </summary>
        /// <param name="site">The IIS Site to configure.</param>
        /// <param name="isStart">Indicates whether the site should be started or not.</param>
        void ConfigureSite(IisSite site, bool isStart);

        /// <summary>
        /// Restarts an IIS Site.
        /// </summary>
        /// <param name="site">The IIS Site to restart.</param>
        void RestartSite(IisSite site);
    }
}
