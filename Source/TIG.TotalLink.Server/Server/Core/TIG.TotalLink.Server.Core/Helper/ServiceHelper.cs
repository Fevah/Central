using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using DevExpress.DataAccess.Native.Sql;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Exceptions;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Helpers;
using DevExpress.Xpo.Metadata;
using TIG.TotalLink.Server.Core.Const;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using TIG.TotalLink.Shared.DataModel.Global;

namespace TIG.TotalLink.Server.Core.Helper
{
    public class ServiceHelper
    {
        #region Public Methods

        /// <summary>
        /// Build a connection string for a database.
        /// </summary>
        /// <param name="provider">Xpo Provider to build a connection string for.</param>
        /// <param name="useServer">Indicates if the provider is server based.</param>
        /// <param name="serverName">Server name for server based databases.</param>
        /// <param name="databaseName">Database name for server based databases.</param>
        /// <param name="databaseFile">Path for file based databases.</param>
        /// <param name="useIntegratedSecurity">Indicates if integrated security should be used.</param>
        /// <param name="userName">Login name for database.</param>
        /// <param name="password">Login password for database.</param>
        /// <returns>A database connection string.</returns>
        public static string GetConnectionString(XpoProvider provider, bool useServer, string serverName, string databaseName, string databaseFile, bool useIntegratedSecurity, string userName, string password)
        {
            try
            {
                // Get a provider factory for the selected provider
                var providerFactory = DataConnectionHelper.GetProviderFactory(provider.Name);
                if (providerFactory == null)
                    return null;

                // Build a dictionary of parameters that are applicable to the selected provider
                var parameters = new Dictionary<string, string>();

                if (useServer)
                {
                    parameters.Add(ProviderFactory.ServerParamID, serverName);
                    parameters.Add(ProviderFactory.DatabaseParamID, databaseName);
                }
                else
                {
                    parameters.Add(ProviderFactory.DatabaseParamID, databaseFile);
                }

                if (useIntegratedSecurity)
                {
                    parameters.Add(ProviderFactory.UseIntegratedSecurityParamID, true.ToString());
                }
                else
                {
                    if (provider.HasUserName)
                        parameters.Add(ProviderFactory.UserIDParamID, userName);
                    if (provider.HasPassword)
                        parameters.Add(ProviderFactory.PasswordParamID, password);
                }

                // Generate a connection string
                var connectionString = providerFactory.GetConnectionString(parameters);

                //// Return the connection string
                //return connectionString;

                // Some provider factories seem to return the wrong XpoProvider in the connection string (e.g. MSSqlServer2005CacheRootProviderFactory)
                // So manually update the connection string with the correct provider key
                var parser = new ConnectionStringParser(connectionString);
                parser.UpdatePartByName("XpoProvider", providerFactory.ProviderKey);

                // Return the connection string
                return parser.GetConnectionString();
            }
            catch (Exception ex)
            {
                throw new FaultException<ServiceFault>(new ServiceFault("Failed to generate connection string!", ex));
            }
        }

        /// <summary>
        /// Updates the database schema.
        /// </summary>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        /// <param name="connectionString">Connection string for the database to update.</param>
        /// <param name="domainAssemblies">Data model assemblies to be applied to the database.</param>
        /// <param name="enableChangeTracking">Indicates if change tracking should be enabled on the database.</param>
        public static void UpdateDatabase(bool performUpdate, string connectionString, Assembly[] domainAssemblies, bool enableChangeTracking = true)
        {
            if (performUpdate)
            {
                // Connect to the database and upgrade it
                IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;
                try
                {
                    // Calling UpdateSchema when the data layer was created with AutoCreateOption.DatabaseAndSchema will create and/or update the database
                    var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.DatabaseAndSchema,
                        domainAssemblies, out objectsToDisposeOnDisconnect);
                    if (dataLayer != null)
                    {
                        using (var uow = new UnitOfWork(dataLayer))
                        {
                            // Update the database
                            uow.UpdateSchema(domainAssemblies);

                            // Enable change tracking on the database
                            if (enableChangeTracking)
                                EnableChangeTracking(uow, connectionString);
                        }

                        // If we reach this point, the database was updated successfully
                    }
                    else
                    {
                        throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                    }
                }
                catch (Exception ex)
                {
                    // Return any error in the result
                    throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
                }
                finally
                {
                    // Dispose of all objects used by the data layer to force it to disconnect
                    if (objectsToDisposeOnDisconnect != null)
                    {
                        foreach (var disposable in objectsToDisposeOnDisconnect)
                        {
                            if (disposable != null)
                                disposable.Dispose();
                        }
                    }
                }
            }
            else
            {
                // Attempt to connect to the database without upgrading, to determine if an upgrade is required
                IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;
                try
                {
                    // Calling UpdateSchema when the data layer was created with AutoCreateOption.None will throw an error if the database needs an update
                    var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.None, domainAssemblies,
                        out objectsToDisposeOnDisconnect);

                    if (dataLayer != null)
                    {
                        using (var uow = new UnitOfWork(dataLayer))
                        {
                            uow.UpdateSchema(domainAssemblies);
                        }

                        // If we reach this point, the database is already up to date
                    }
                    else
                    {
                        throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                    }
                }
                catch (SchemaCorrectionNeededException ex)
                {
                    // This error will occur if the database requires an update
                    throw new FaultException<ServiceFault>(new ServiceFault(ex.Message.Replace(" Sql text: ", "\r\n"), ex));
                }
                catch (Exception ex)
                {
                    // Return any unexpected error in the result
                    throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
                }
                finally
                {
                    // Dispose of all objects used by the data layer to force it to disconnect
                    if (objectsToDisposeOnDisconnect != null)
                    {
                        foreach (var disposable in objectsToDisposeOnDisconnect.Where(disposable => disposable != null))
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Populates defaults for all entities.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to populate.</param>
        /// <param name="domainAssemblies">Data model assemblies to be applied to the database.</param>
        public static void PopulateDataStore(string connectionString, Assembly[] domainAssemblies)
        {
            IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;
            try
            {
                // Attempt to connect to the database
                var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.SchemaAlreadyExists, domainAssemblies,
                    out objectsToDisposeOnDisconnect);

                if (dataLayer != null)
                {
                    // Call PopulateDataStore on each class which implements IPopulateDataStore (within the domain assemblies)
                    foreach (var assembly in domainAssemblies)
                    {
                        var populateTypes = assembly.GetTypes().Where(t => typeof(IPopulateDataStore).IsAssignableFrom(t));
                        foreach (var type in populateTypes)
                        {
                            var populateInstance = (IPopulateDataStore)Activator.CreateInstance(type);
                            populateInstance.PopulateDataStore(dataLayer);
                        }
                    }
                }
                else
                {
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                }
            }
            catch (Exception ex)
            {
                // Return any unexpected error in the result
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }
            finally
            {
                // Dispose of all objects used by the data layer to force it to disconnect
                if (objectsToDisposeOnDisconnect != null)
                {
                    foreach (var disposable in objectsToDisposeOnDisconnect)
                    {
                        if (disposable != null)
                            disposable.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Purges all deleted objects.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to purge.</param>
        /// <param name="domainAssemblies">Data model assemblies to be applied to the database.</param>
        public static PurgeResult PurgeDatabase(string connectionString, Assembly[] domainAssemblies)
        {
            PurgeResult result;

            IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;
            try
            {
                // Attempt to connect to the database
                var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.None, domainAssemblies, out objectsToDisposeOnDisconnect);

                if (dataLayer != null)
                {
                    // Purge the deleted objects
                    using (var uow = new UnitOfWork(dataLayer))
                    {
                        result = uow.PurgeDeletedObjects();
                    }
                }
                else
                {
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                }
            }
            catch (Exception ex)
            {
                // Return any unexpected error in the result
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }
            finally
            {
                // Dispose of all objects used by the data layer to force it to disconnect
                if (objectsToDisposeOnDisconnect != null)
                {
                    foreach (var disposable in objectsToDisposeOnDisconnect)
                    {
                        if (disposable != null)
                            disposable.Dispose();
                    }
                }
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Creates and returns a data layer that connects to a database data store.
        /// </summary>
        /// <param name="connectionString">The connectionString to use when connect to database.</param>
        /// <param name="autoCreateOption">The AutoCreateOption to use when connecting to the database.</param>
        /// <param name="domainAssemblies">An array of assemblies that contain the datamodels for this database.</param>
        /// <param name="objectsToDisposeOnDisconnect">Returns all objects that are used by this data layer that need to be disposed of in order to disconnect cleanly.</param>
        /// <returns>The new data layer.</returns>
        public static IDataLayer CreateDatabaseDataLayer(string connectionString, AutoCreateOption autoCreateOption, Assembly[] domainAssemblies, out IEnumerable<IDisposable> objectsToDisposeOnDisconnect)
        {
            // Create a dictionary that contains all XPO types that exist in the module assemblies
            var dict = DataModelHelper.GetReflectionDictionary(domainAssemblies);

            // Attempt to get a data store based on the connection settings
            IDisposable[] providersDisposable;
            var dataStore = XpoDefault.GetConnectionProvider(connectionString, autoCreateOption, out providersDisposable);
            if (dataStore == null)
            {
                objectsToDisposeOnDisconnect = null;
                return null;
            }

            // Create and return a data layer
            var dataLayer = new SimpleDataLayer(dict, dataStore);

            // Return the objects to dispose
            var disposables = new List<IDisposable>();
            disposables.AddRange(providersDisposable);
            disposables.Add(dataLayer);
            objectsToDisposeOnDisconnect = disposables;

            // Return the data layer
            return dataLayer;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Enables change tracking on a database.
        /// </summary>
        /// <param name="uow">The UnitOfWork to execute in.</param>
        /// <param name="connectionString">The original connection string for the database.  Used to extract database information.</param>
        private static void EnableChangeTracking(UnitOfWork uow, string connectionString)
        {
            // Collect useful values
            var parser = new ConnectionStringParser(connectionString);
            var provider = parser.GetPartByName("XpoProvider").Replace("2005CacheRoot", "").Replace("2005WithCache", "");
            var dbName = parser.GetPartByName("initial catalog");
            var resourceRootPath = Path.Combine("Resources", provider);

            // Build an initial dictionary of aliases
            var aliases = new Dictionary<string, string>
            {
                { SqlAliases.DbName, dbName },
                { SqlAliases.TableName, null }
            };

            // Enable change tracking on the database
            try
            {
                ExecuteSql(uow, Path.Combine(resourceRootPath, "EnableDatabaseChangeTracking.sql"), aliases);
            }
            catch (SqlExecutionErrorException ex)
            {
                if (ex.InnerException != null && ex.InnerException.Message.StartsWith("Change tracking is already enabled for database"))
                {
                    // Ignore errors about change tracking already being enabled
                }
                else
                {
                    // Any other errors will be re-thrown
                    throw;
                }
            }

            // Get a list of persistent classes, and enable change tracking for each related table
            var persistentClasses = uow.Dictionary.Classes.Cast<ReflectionClassInfo>().Where(c => c.IsPersistent).ToList();
            foreach (var classInfo in persistentClasses)
            {
                try
                {
                    aliases[SqlAliases.TableName] = classInfo.TableName;
                    ExecuteSql(uow, Path.Combine(resourceRootPath, "EnableTableChangeTracking.sql"), aliases);
                }
                catch (SqlExecutionErrorException ex)
                {
                    if (ex.InnerException != null && ex.InnerException.Message.StartsWith("Change tracking is already enabled for table"))
                    {
                        // Ignore errors about change tracking already being enabled
                    }
                    else
                    {
                        // Any other errors will be re-thrown
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Executes an SQL script that is stored as an embedded resource.
        /// </summary>
        /// <param name="uow">The UnitOfWork to execute in.</param>
        /// <param name="path">The path to the SQL script to execute.</param>
        /// <param name="aliases">A dictionary of aliased values to replace before executing the script.</param>
        private static void ExecuteSql(UnitOfWork uow, string path, Dictionary<string, string> aliases)
        {
            // Attempt to load the sql script resource
            string sql;
            try
            {
                sql = DataModelHelper.ReadResourceContent(path);
            }
            catch (Exception)
            {
                // Abort if the script file was not found
                return;
            }

            // Replace aliases in the sql
            sql = aliases.Aggregate(sql, (current, alias) => current.Replace(alias.Key, alias.Value));

            // Execute the sql
            uow.ExecuteNonQuery(sql);
        }

        #endregion
    }
}
