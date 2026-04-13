using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using DevExpress.Xpo;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.Core.Configuration;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.Contract.Global;
using TIG.TotalLink.Shared.DataModel.Core.Attribute;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Global;
using TIG.TotalLink.Shared.Xpo.Core.Helper;

namespace TIG.TotalLink.Server.MethodService.Global
{
    // TODO : Security is badly needed in here to ensure that only the Server Manager can call these methods

    public class GlobalMethodService : MethodServiceBase, IGlobalMethodService
    {
        #region Private Fields

        private static readonly Assembly[] _dataModelAssemblies;
        private static IGlobalFacade _globalFacade;

        #endregion


        #region Constructors

        static GlobalMethodService()
        {
            // Load all assemblies in the Module directory
            var dataModelAssemblies = PreLoadAssemblies(Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin"), "Module"));

            // Also add the assembly that XPObject type exists in because XPO always requires it
            dataModelAssemblies.Add(typeof(XPObjectType).Assembly);

            // Store the assemblies in an array for use later
            _dataModelAssemblies = dataModelAssemblies.ToArray();
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets a connection string for the specified database domain.
        /// </summary>
        /// <returns>The connection string.</returns>
        public string GetConnectionString(DatabaseDomain domain)
        {
            // Get the GlobalFacade
            var globalFacade = GetGlobalDataFacade();

            // Collect all relevant settings from the global facade
            XpoProvider provider = null;
            bool useIntegratedSecurity;
            bool useServer;

            Guid providerId;
            if (Guid.TryParse(globalFacade.GetDatabaseSetting(domain, "Provider"), out providerId))
            {
                provider = (globalFacade.ExecuteQuery(uow =>
                    uow.Query<XpoProvider>().Where(p => p.Oid == providerId)
                )).FirstOrDefault();
            }

            // Abort if no provider is set
            if (provider == null)
                return null;

            var serverName = globalFacade.GetDatabaseSetting(domain, "ServerName");
            var databaseName = globalFacade.GetDatabaseSetting(domain, "DatabaseName");
            var databaseFile = globalFacade.GetDatabaseSetting(domain, "DatabaseFile");
            bool.TryParse(globalFacade.GetDatabaseSetting(domain, "UseIntegratedSecurity"), out useIntegratedSecurity);
            var userName = globalFacade.GetDatabaseSetting(domain, "UserName");
            var password = globalFacade.GetDatabaseSetting(domain, "Password");
            bool.TryParse(globalFacade.GetDatabaseSetting(domain, "UseServer"), out useServer);

            return ServiceHelper.GetConnectionString(provider.Name, provider.HasUserName, provider.HasPassword, useServer, serverName, databaseName, databaseFile, useIntegratedSecurity, userName, password);
        }

        /// <summary>
        /// Updates the database schema.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the update on.</param>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        public void UpdateDatabase(DatabaseDomain databaseDomain, bool performUpdate)
        {
            // Get the connection string for the specified database domain
            var connectionString = GetConnectionString(databaseDomain);

            // Get all datamodel assemblies for the specified database domain
            var domainAssemblies = GetDomainAssemblies(databaseDomain);

            // Update the database
            ServiceHelper.UpdateDatabase(performUpdate, connectionString, domainAssemblies, databaseDomain == DatabaseDomain.Main);
        }

        /// <summary>
        /// Purges deleted items from the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the purge on.</param>
        public PurgeResult PurgeDatabase(DatabaseDomain databaseDomain)
        {
            // Get all datamodel assemblies for the specified database domain
            var domainAssemblies = GetDomainAssemblies(databaseDomain);

            // Get the connection string for the specified database domain
            var connectionString = GetConnectionString(databaseDomain);

            // Purge the database
            return ServiceHelper.PurgeDatabase(connectionString, domainAssemblies);
        }

        /// <summary>
        /// Populates defaults for all entities.
        /// </summary>
        /// <param name="databaseDomain">The database domain to populate.</param>
        public void PopulateDataStore(DatabaseDomain databaseDomain)
        {
            // Get all datamodel assemblies for the specified database domain
            var domainAssemblies = GetDomainAssemblies(databaseDomain);

            // Get the connection string for the specified database domain
            var connectionString = GetConnectionString(databaseDomain);

            // Populate the data store
            ServiceHelper.PopulateDataStore(connectionString, domainAssemblies);
        }

        /// <summary>
        /// Exports a table directly from the database to an xml file.
        /// </summary>
        /// <param name="databaseDomain">The database domain to export from.</param>
        /// <param name="exportPath">
        /// The path to save the export file in.
        /// Any existing files in this folder may be overwritten.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to export.</param>
        public void ExportTable(DatabaseDomain databaseDomain, string exportPath, string typeName)
        {
            // Get the connection string for the specified database domain
            var connectionString = GetConnectionString(databaseDomain);

            // Export the table
            ServiceHelper.ExportTable(connectionString, exportPath, typeName);
        }

        /// <summary>
        /// Imports a table directly from an xml file into the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to import into.</param>
        /// <param name="importPath">
        /// The path to load the import file from.
        /// This folder should contain an xml file with the same name as the table being imported.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to import.</param>
        public void ImportTable(DatabaseDomain databaseDomain, string importPath, string typeName)
        {
            // Get the connection string for the specified database domain
            var connectionString = GetConnectionString(databaseDomain);

            // Special handling for the User table
            if (typeName.ToLower().EndsWith(".user"))
            {
                ServiceHelper.ImportUserTable(connectionString, importPath, typeName);
                return;
            }

            // Everything but the User table can be imported using the standard method
            ServiceHelper.ImportTable(connectionString, importPath, typeName);
        }

        /// <summary>
        /// Empties tables by deleting all rows.
        /// Tables will be emptied in the order they are listed, so ensure they are ordered based on dependencies.
        /// </summary>
        /// <param name="databaseDomain">The database domain to empty tables in.</param>
        /// <param name="tableNames">An array of the table names to empty.</param>
        public void EmptyTables(DatabaseDomain databaseDomain, string[] tableNames)
        {
            // Get the connection string for the specified database domain
            var connectionString = GetConnectionString(databaseDomain);

            // Empty the tables
            ServiceHelper.EmptyTables(connectionString, tableNames);
        }

        /// <summary>
        /// Returns information about export files in the specified path.
        /// </summary>
        /// <param name="exportPath">The path to search for export files in.</param>
        /// <returns>A ListExportFilesResult object containing information about export files in the specified path.</returns>
        public ListExportFilesResult ListExportFiles(string exportPath)
        {
            // Get the connection string for the Main database domain
            var connectionString = GetConnectionString(DatabaseDomain.Main);

            // Get information about the export files
            var data = ServiceHelper.ListExportFiles(connectionString, exportPath);

            // If the results contained an error, throw it back to the client
            if (data.ResultSet[1].Rows.Length > 0 && !string.IsNullOrWhiteSpace(data.ResultSet[1].Rows[0].Values[0] as string))
                throw new FaultException<ServiceFault>(new ServiceFault((string)data.ResultSet[1].Rows[0].Values[0]));

            // Format and return the results
            return new ListExportFilesResult()
            {
                ExportFiles = data.ResultSet[0].Rows.Select(r => new ExportFileResult() { TableName = r.Values[0] as string, Version = r.Values[1] as string }).ToArray()
            };
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Initializes and returns a GlobalFacade connected to the data service only.
        /// </summary>
        /// <returns>A GlobalFacade.</returns>
        private static IGlobalFacade GetGlobalDataFacade()
        {
            if (_globalFacade == null)
                _globalFacade = new GlobalFacade(new ServerServiceConfiguration(DefaultUserCache.LoginServiceUser(DefaultUserCache.ServiceToServiceUserName)));

            try
            {
                if (_globalFacade != null && !_globalFacade.IsDataConnected)
                    _globalFacade.Connect(ServiceTypes.Data);
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to connect to Global Facade!", ex));
            }

            return _globalFacade;
        }

        /// <summary>
        /// Pre-loads assemblies.
        /// </summary>
        /// <param name="path">The path to load assemblies from.</param>
        /// <returns>A list of the assemblies that were loaded.</returns>
        private static List<Assembly> PreLoadAssemblies(string path)
        {
            var assemblies = new List<Assembly>();

            // Abort if the directory doesn't exist
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return assemblies;

            // Get a list of the assemblies to load
            var files = new DirectoryInfo(path).GetFiles("*.dll", SearchOption.TopDirectoryOnly);

            // Abort if there are no files to process
            if (files.Length == 0)
                return assemblies;

            // Process all files in the directory
            foreach (var file in files)
            {
                // Load the assembly if it isn't already loaded
                var assemblyName = AssemblyName.GetAssemblyName(file.FullName);
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(assemblyName, a.GetName()));
                if (assembly != null)
                {
                    System.Diagnostics.Debug.WriteLine("PreLoadAssemblies: Found {0}", new object[] { assemblyName.Name });
                    assemblies.Add(assembly);
                }
                else
                {
                    try
                    {
                        assemblies.Add(Assembly.Load(assemblyName));
                        System.Diagnostics.Debug.WriteLine("PreLoadAssemblies: Loaded {0}", new object[] { assemblyName.Name });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("PreLoadAssemblies: Error loading {0}\r\n{1}", assemblyName.Name, ex);
                    }
                }
            }

            return assemblies;
        }

        /// <summary>
        /// Gets all assemblies which relate to the specified database domain.
        /// </summary>
        /// <param name="databaseDomain">The database domain to get assemblies for.</param>
        /// <returns>An array of assemblies which relate to the specified database domain.</returns>
        private static Assembly[] GetDomainAssemblies(DatabaseDomain databaseDomain)
        {
            try
            {
                // Return all datamodel assemblies for the specified database domain
                return _dataModelAssemblies.Where(a =>
                         (a.IsDefined(typeof(DatabaseDomainAttribute))
                         && ((DatabaseDomainAttribute)a.GetCustomAttribute(typeof(DatabaseDomainAttribute))).Domain == databaseDomain)
                         || a.FullName.StartsWith("DevExpress")
                ).ToArray();
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to collect assemblies for database domain {0}!", databaseDomain), ex));
            }
        }

        #endregion
    }
}
