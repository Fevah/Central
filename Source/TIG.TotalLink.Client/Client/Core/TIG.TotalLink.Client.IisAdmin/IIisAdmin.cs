using System.ServiceModel;

namespace TIG.TotalLink.Client.IisAdmin
{
    [ServiceContract]
    public interface IIisAdmin
    {
        /// <summary>
        /// Dummy method for clients to use for testing connectivity.
        /// </summary>
        [OperationContract]
        void Ping();

        /// <summary>
        /// Gets all sites in IIS.
        /// </summary>
        /// <returns>An array containing all sites in IIS.</returns>
        [OperationContract]
        IisSite[] GetSites();

        /// <summary>
        /// Starts or stops a site in IIS.
        /// </summary>
        /// <param name="siteId">Id of the site to start or stop.</param>
        /// <param name="isStart">True to start the site, or false to stop it.</param>
        /// <returns>True if the site was started or stopped successfully.</returns>
        [OperationContract]
        bool ConfigureSite(long siteId, bool isStart);
    }
}
