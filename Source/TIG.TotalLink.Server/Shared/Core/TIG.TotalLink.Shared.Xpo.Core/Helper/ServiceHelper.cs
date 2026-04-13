using System;
using System.Collections.Generic;
using System.Globalization;
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
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Core;
using TIG.TotalLink.Shared.DataModel.Core.Helper;
using System.Data.Entity.Design.PluralizationServices;
using System.ServiceModel.Web;
using TIG.TotalLink.Shared.Xpo.Core.Const;

namespace TIG.TotalLink.Shared.Xpo.Core.Helper
{
    public class ServiceHelper
    {
        #region Private Fields

        private static PluralizationService _pluralizationService;
        public static readonly List<string> ExcludedImportExportProperties = new List<string>() { "OptimisticLockField", "GCRecord" };

        #endregion


        #region Public Methods

        /// <summary>
        /// Build a connection string for a database.
        /// </summary>
        /// <param name="providerName">Xpo Provider Name to build a connection string for.</param>
        /// <param name="hasUserName">Indicate Provider has User Name.</param>
        /// <param name="hasPassword">Indicate Provider has password.</param>
        /// <param name="useServer">Indicates if the provider is server based.</param>
        /// <param name="serverName">Server name for server based databases.</param>
        /// <param name="databaseName">Database name for server based databases.</param>
        /// <param name="databaseFile">Path for file based databases.</param>
        /// <param name="useIntegratedSecurity">Indicates if integrated security should be used.</param>
        /// <param name="userName">Login name for database.</param>
        /// <param name="password">Login password for database.</param>
        /// <returns>A database connection string.</returns>
        public static string GetConnectionString(string providerName, bool hasUserName, bool hasPassword, bool useServer, string serverName, string databaseName, string databaseFile, bool useIntegratedSecurity, string userName, string password)
        {
            try
            {
                // Get a provider factory for the selected provider
                var providerFactory = DataConnectionHelper.GetProviderFactory(providerName);
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
                    if (hasUserName)
                        parameters.Add(ProviderFactory.UserIDParamID, userName);
                    if (hasPassword)
                        parameters.Add(ProviderFactory.PasswordParamID, password);
                }

                // Generate a connection string
                var connectionString = providerFactory.GetConnectionString(parameters);

                // Some provider factories seem to return the wrong XpoProvider in the connection string (e.g. MSSqlServer2005CacheRootProviderFactory)
                // So manually update the connection string with the correct provider key
                var parser = new ConnectionStringParser(connectionString);
                parser.UpdatePartByName("XpoProvider", providerFactory.ProviderKey);

                // Return the connection string
                return parser.GetConnectionString();
            }
            catch (FaultException)
            {
                throw;
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
                    var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.DatabaseAndSchema, domainAssemblies, out objectsToDisposeOnDisconnect);
                    if (dataLayer != null)
                    {
                        using (var uow = new UnitOfWork(dataLayer))
                        {
                            // Update the database
                            uow.UpdateSchema(domainAssemblies);

                            // Enable the xp_cmdshell feature on the database server
                            EnableXpCmdShell(uow, connectionString);

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
                catch (FaultException)
                {
                    throw;
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
                    var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.None, domainAssemblies, out objectsToDisposeOnDisconnect);

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
                catch (FaultException)
                {
                    throw;
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
                var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.SchemaAlreadyExists, domainAssemblies, out objectsToDisposeOnDisconnect);

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
            catch (FaultException)
            {
                throw;
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
            catch (FaultException)
            {
                throw;
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
        /// Empties tables by deleting all rows.
        /// Tables will be emptied in the order they are listed, so ensure they are ordered based on dependencies.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to empty tables in.</param>
        /// <param name="tableNames">An array of the table names to empty.</param>
        public static void EmptyTables(string connectionString, string[] tableNames)
        {
            // Collect useful values
            var parser = new ConnectionStringParser(connectionString);
            var provider = parser.GetPartByName("XpoProvider").Replace("2005CacheRoot", "").Replace("2005WithCache", "");
            var resourceRootPath = Path.Combine("Resources", provider);

            IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;
            try
            {
                // Attempt to connect to the database
                var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.None, new Assembly[] { }, out objectsToDisposeOnDisconnect);

                if (dataLayer != null)
                {
                    // Empty the specified tables
                    using (var uow = new UnitOfWork(dataLayer))
                    {
                        foreach (var tableName in tableNames)
                        {
                            ExecuteSql(uow, Path.Combine(resourceRootPath, "EmptyTable.sql"), new Dictionary<string, string> { { SqlAliases.TableName, tableName } });
                        }
                    }
                }
                else
                {
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                }
            }
            catch (FaultException)
            {
                throw;
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
        /// Exports a table directly from the database to an xml file.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to export from.</param>
        /// <param name="exportPath">
        /// The path to save the export file in.
        /// Any existing files in this folder may be overwritten.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to export.</param>
        public static void ExportTable(string connectionString, string exportPath, string typeName)
        {
            // Attempt to get the type being exported
            var type = DataModelHelper.GetTypeFromLoadedAssemblies(typeName);
            if (type == null)
                throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to find type {0}!", typeName)));

            // Build a list of columns to be exported
            var dictionary = DataModelHelper.GetReflectionDictionary(type.Assembly);
            var classInfo = dictionary.GetClassInfo(type);
            var columnList = string.Join(",",
                classInfo.PersistentProperties
                    .Cast<XPMemberInfo>()
                    .Where(m => !ExcludedImportExportProperties.Contains(m.MappingField))
                    .Select(m => string.Format("[{0}]", m.MappingField))
            );

            // Build a dictionary of aliases
            var parser = new ConnectionStringParser(connectionString);
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var aliases = new Dictionary<string, string>
            {
                { SqlAliases.FileName, Path.Combine(exportPath, string.Format("{0}.xml", classInfo.TableName)) },
                { SqlAliases.ServerName, parser.GetPartByName("data source") },
                { SqlAliases.DbName, parser.GetPartByName("initial catalog") },
                { SqlAliases.UserName, parser.GetPartByName("user id") },
                { SqlAliases.Password, parser.GetPartByName("password") },
                { SqlAliases.TableName, classInfo.TableName },
                { SqlAliases.TableNamePlural, GetPluralizationService().Pluralize(classInfo.TableName) },
                { SqlAliases.ColumnList, columnList },
                { SqlAliases.Version, string.Format("{0}.{1:00}", version.Major, version.Minor) }
            };

            // Execute the SQL command
            ExecuteSql(connectionString, "ExportTable", aliases);
        }

        /// <summary>
        /// Imports a table directly from an xml file into the database.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to import into.</param>
        /// <param name="importPath">
        /// The path to load the import file from.
        /// This folder should contain an xml file with the same name as the table being imported.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to import.</param>
        public static void ImportTable(string connectionString, string importPath, string typeName)
        {
            var objectsToDisposeOnDisconnect = new List<IDisposable>();

            try
            {
                // Attempt to get the type being imported
                var type = DataModelHelper.GetTypeFromLoadedAssemblies(typeName);
                if (type == null)
                    throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to find type {0}!", typeName)));

                // Replace the provider in the connection string with a non-cached version
                var parser = new ConnectionStringParser(connectionString);
                parser.UpdatePartByName("XpoProvider", parser.GetPartByName("XpoProvider").Replace("2005CacheRoot", "").Replace("2005WithCache", ""));

                // Attempt to get a data store based on the connection settings
                IDisposable[] providersDisposable;
                var dataStore = XpoDefault.GetConnectionProvider(parser.GetConnectionString(), AutoCreateOption.None, out providersDisposable) as ConnectionProviderSql;
                if (dataStore == null)
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to create data store!"));
                objectsToDisposeOnDisconnect.AddRange(providersDisposable);

                // Get information about the class type
                var dictionary = DataModelHelper.GetReflectionDictionary(type.Assembly);
                var classInfo = dictionary.GetClassInfo(type);

                // Attempt to get information about the database table
                var table = dataStore.GetStorageTables(classInfo.TableName).FirstOrDefault();
                if (table == null)
                    throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to collect details for table {0}!", classInfo.TableName)));

                // Populate the DBTypeName in the DBColumns
                var getSqlCreateColumnTypeMethod = dataStore.GetType().GetMethod("GetSqlCreateColumnType", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var dbColumn in table.Columns)
                {
                    dbColumn.DBTypeName = getSqlCreateColumnTypeMethod.Invoke(dataStore, new object[] { table, dbColumn }) as string;
                }

                // Build a list of columns to be imported
                var columnList = string.Join(",",
                    classInfo.PersistentProperties
                        .Cast<XPMemberInfo>()
                        .Where(m => !ExcludedImportExportProperties.Contains(m.MappingField))
                        .Select(m => string.Format("[{0}]", m.MappingField))
                    );
                var columnListWithConversions = string.Join(",",
                    classInfo.PersistentProperties
                        .Cast<XPMemberInfo>()
                        .Where(m => !ExcludedImportExportProperties.Contains(m.MappingField))
                        .Select(m =>
                        {
                            var dbTypeName = table.GetColumn(m.MappingField).DBTypeName.ToLower();
                            switch (dbTypeName)
                            {
                                case "varbinary(max)":
                                    return string.Format("[{0}].value('.', 'varbinary(max)')", m.MappingField);
                            }
                            return string.Format("[{0}]", m.MappingField);
                        })
                    );
                var columnListWithDataTypes = string.Join(",",
                    classInfo.PersistentProperties
                        .Cast<XPMemberInfo>()
                        .Where(m => !ExcludedImportExportProperties.Contains(m.MappingField))
                        .Select(m =>
                        {
                            var dbTypeName = table.GetColumn(m.MappingField).DBTypeName.ToLower();
                            switch (dbTypeName)
                            {
                                case "varbinary(max)":
                                    dbTypeName = "xml";
                                    break;
                            }
                            return string.Format("[{0}] {1}", m.MappingField, dbTypeName);
                        })
                    );

                // Build a dictionary of aliases
                var aliases = new Dictionary<string, string>
                {
                    {SqlAliases.FileName, Path.Combine(importPath, string.Format("{0}.xml", classInfo.TableName))},
                    {SqlAliases.ColumnList, columnList},
                    {SqlAliases.ColumnListWithConversions, columnListWithConversions},
                    {SqlAliases.ColumnListWithDataTypes, columnListWithDataTypes},
                    {SqlAliases.TableName, classInfo.TableName},
                    {SqlAliases.TableNamePlural, GetPluralizationService().Pluralize(classInfo.TableName)}
                };

                // Execute the SQL command
                ExecuteSql(connectionString, "ImportTable", aliases);
            }
            catch (FaultException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Return any unexpected error in the result
                throw new FaultException<ServiceFault>(new ServiceFault(ex.Message, ex));
            }
            finally
            {
                // Dispose of all objects used by the data layer to force it to disconnect
                foreach (var disposable in objectsToDisposeOnDisconnect)
                {
                    if (disposable != null)
                        disposable.Dispose();
                }
            }
        }

        /// <summary>
        /// Imports a User table directly from an xml file into the database by merging it with the existing users.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to import into.</param>
        /// <param name="importPath">
        /// The path to load the import file from.
        /// This folder should contain an xml file with the same name as the table being imported.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to import.</param>
        public static void ImportUserTable(string connectionString, string importPath, string typeName)
        {
            // Attempt to get the type being imported
            var type = DataModelHelper.GetTypeFromLoadedAssemblies(typeName);
            if (type == null)
                throw new FaultException<ServiceFault>(new ServiceFault(string.Format("Failed to find type {0}!", typeName)));

            // Get information about the class type
            var dictionary = DataModelHelper.GetReflectionDictionary(type.Assembly);
            var classInfo = dictionary.GetClassInfo(type);

            // Execute the SQL command
            ExecuteSql(connectionString, "ImportUserTable", new Dictionary<string, string> { { SqlAliases.FileName, Path.Combine(importPath, string.Format("{0}.xml", classInfo.TableName)) } });
        }

        /// <summary>
        /// Returns information about export files in the specified path.
        /// </summary>
        /// <param name="connectionString">Connection string for the database to export from.</param>
        /// <param name="exportPath">The path to search for export files in.</param>
        /// <returns>A SelectedData object containing information about export files in the specified path.</returns>
        public static SelectedData ListExportFiles(string connectionString, string exportPath)
        {
            return ExecuteSql(connectionString, "ListExportFiles", new Dictionary<string, string> { { SqlAliases.Path, exportPath } }, true);
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

        /// <summary>
        /// Gets the current authentication token from the request headers.
        /// </summary>
        /// <returns>The current authentication token.</returns>
        public static string GetCurrentAuthenticationToken()
        {
            // Attempt to get the current WebOperationContext
            var webOperationContext = WebOperationContext.Current;
            if (webOperationContext == null)
                return null;

            // Return the value of the AuthenticationToken header
            return webOperationContext.IncomingRequest.Headers["AuthenticationToken"];
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
            var persistentClasses = uow.Dictionary.Classes.Cast<XPClassInfo>().Where(c => c.IsPersistent).ToList();
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
        /// Enables the xp_cmdshell feature on the database server.
        /// </summary>
        /// <param name="uow">The UnitOfWork to execute in.</param>
        /// <param name="connectionString">The original connection string for the database.  Used to extract database information.</param>
        private static void EnableXpCmdShell(UnitOfWork uow, string connectionString)
        {
            // Collect useful values
            var parser = new ConnectionStringParser(connectionString);
            var provider = parser.GetPartByName("XpoProvider").Replace("2005CacheRoot", "").Replace("2005WithCache", "");
            var resourceRootPath = Path.Combine("Resources", provider);

            // Enable XpCmdShell
            ExecuteSql(uow, Path.Combine(resourceRootPath, "EnableXpCmdShell.sql"));
        }

        /// <summary>
        /// Executes an SQL script that is stored as an embedded resource.
        /// </summary>
        /// <param name="uow">The UnitOfWork to execute in.</param>
        /// <param name="path">The path to the SQL script to execute.</param>
        /// <param name="aliases">A dictionary of aliased values to replace before executing the script.</param>
        /// <param name="returnsResults">Indicates if the query is expected to return a result.</param>
        /// <returns>A SelectedData object if returnsResult = true; otherwise null.</returns>
        private static SelectedData ExecuteSql(UnitOfWork uow, string path, Dictionary<string, string> aliases = null, bool returnsResults = false)
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
                return null;
            }

            // Replace aliases in the sql
            if (aliases != null)
                sql = aliases.Aggregate(sql, (current, alias) => current.Replace(alias.Key, alias.Value));

            // Execute the sql if it returns results
            if (returnsResults)
                return uow.ExecuteQuery(sql);

            // Execute the sql if it doesn't return results
            uow.ExecuteNonQuery(sql);
            return null;
        }

        /// <summary>
        /// Executes an SQL script that is stored as an embedded resource.
        /// Note that this script will be executed on a new datalayer which contains no data models.
        /// </summary>
        /// <param name="connectionString">The original connection string for the database.  Used to extract database information.</param>
        /// <param name="name">The name of the SQL script to execute.</param>
        /// <param name="aliases">A dictionary of aliased values to replace before executing the script.</param>
        /// <param name="returnsResults">Indicates if the query is expected to return a result.</param>
        /// <returns>A SelectedData object if returnsResult = true; otherwise null.</returns>
        private static SelectedData ExecuteSql(string connectionString, string name, Dictionary<string, string> aliases = null, bool returnsResults = false)
        {
            // Collect useful values
            var parser = new ConnectionStringParser(connectionString);
            var provider = parser.GetPartByName("XpoProvider").Replace("2005CacheRoot", "").Replace("2005WithCache", "");
            var resourceRootPath = Path.Combine("Resources", provider);

            IEnumerable<IDisposable> objectsToDisposeOnDisconnect = null;
            SelectedData result;
            try
            {
                // Attempt to connect to the database
                var dataLayer = CreateDatabaseDataLayer(connectionString, AutoCreateOption.None, new Assembly[] { }, out objectsToDisposeOnDisconnect);

                if (dataLayer != null)
                {
                    // Execute the specified sql
                    using (var uow = new UnitOfWork(dataLayer))
                    {
                        result = ExecuteSql(uow, Path.Combine(resourceRootPath, string.Format("{0}.sql", name)), aliases, returnsResults);
                    }
                }
                else
                {
                    throw new FaultException<ServiceFault>(new ServiceFault("Failed to initialize data layer!"));
                }
            }
            catch (FaultException)
            {
                throw;
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

            return result;
        }

        private static PluralizationService GetPluralizationService()
        {
            return _pluralizationService ?? (_pluralizationService = PluralizationService.CreateService(new CultureInfo("en-US")));
        }

        #endregion
    }
}
