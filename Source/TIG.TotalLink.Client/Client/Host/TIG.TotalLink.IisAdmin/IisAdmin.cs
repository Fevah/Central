using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Microsoft.Web.Administration;
using TIG.TotalLink.Client.IisAdmin;

namespace TIG.TotalLink.IisAdmin
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class IisAdmin : IIisAdmin
    {
        #region Private Fields

        private ServerManager _serverManager;

        #endregion


        #region Public Properties

        /// <summary>
        /// The server manager used to manage the IIS settings.
        /// </summary>
        public ServerManager ServerManager
        {
            get
            {
                // Initialize the servermanger if its null
                if (_serverManager == null)
                {
                    _serverManager = IsExpress
                        ? new ServerManager(string.Format(@"{0}\IISExpress\config\applicationhost.config", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)))
                        : new ServerManager();
                }

                return _serverManager;
            }
        }

        #endregion


        #region Public Properties

        public static bool IsExpress { get; set; }

        #endregion


        #region Public Methods

        /// <summary>
        /// Dummy method for clients to use for testing connectivity.
        /// </summary>
        public void Ping()
        {
        }

        /// <summary>
        /// Gets all sites in IIS.
        /// </summary>
        /// <returns>An array containing all sites in IIS.</returns>
        public IisSite[] GetSites()
        {
            // Create a new list of IisSites
            var iisSites = new List<IisSite>();

            // Process each site returned by the ServerManager
            foreach (var site in ServerManager.Sites)
            {
                // Ignore IIS sites whose names don't match "TotalLink * Data Service" or "TotalLink * Method Service"
                if (!IsExpress && !(site.Name.StartsWith("TotalLink ") && (site.Name.EndsWith(" Data Service") || site.Name.EndsWith(" Method Service"))))
                    continue;

                // Ignore IIS Express sites whose names don't match "TIG.TotalLink.Server.DataService.*" or "TIG.TotalLink.Server.MethodService.*"
                if (IsExpress && !site.Name.StartsWith("TIG.TotalLink.Server.DataService.") && !site.Name.StartsWith("TIG.TotalLink.Server.MethodService."))
                    continue;

                // Create a new IisSite
                var iisSite = new IisSite() { Id = site.Id, Name = site.Name };
                
                // Attempt to populate the Port
                if (site.Bindings.Any())
                {
                    iisSite.Port = site.Bindings[0].EndPoint.Port;
                }

                // Attempt to populate the State
                try
                {
                    iisSite.State = site.State;
                }
                catch
                {
                    iisSite.State = ObjectState.Unknown;
                }

                // Attempt to populate the ApplicationPool
                if (site.Applications.Any())
                {
                    iisSite.ApplicationPool = site.Applications[0].ApplicationPoolName;
                }

                // Add the IisSite to the list
                iisSites.Add(iisSite);
            }

            // Return the list as an array
            return iisSites.ToArray();
        }

        /// <summary>
        /// Starts or stops a site in IIS.
        /// </summary>
        /// <param name="siteId">Id of the site to start or stop.</param>
        /// <param name="isStart">True to start the site, or false to stop it.</param>
        /// <returns>True if the site was started or stopped successfully.</returns>
        public bool ConfigureSite(long siteId, bool isStart)
        {
            // Attempt to find the site by id
            var site = ServerManager.Sites.FirstOrDefault(s => s.Id == siteId);

            // Abort if no site was found
            if (site == null)
                return false;

            // Start or stop the site
            try
            {
                if (isStart)
                    site.Start();
                else
                    site.Stop();

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
