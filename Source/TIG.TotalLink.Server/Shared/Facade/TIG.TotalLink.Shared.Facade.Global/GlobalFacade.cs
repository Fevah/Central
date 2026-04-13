using System.Linq;
using System.Threading.Tasks;
using DevExpress.Xpo;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Shared.Contract.Global;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Global;
using TIG.TotalLink.Shared.Facade.Core;
using TIG.TotalLink.Shared.Facade.Core.Attribute;
using TIG.TotalLink.Shared.Facade.Core.Configuration;

namespace TIG.TotalLink.Shared.Facade.Global
{
    [Facade(0, "Global", 0, "Global")]
    public class GlobalFacade : FacadeBase<XpoProvider, IGlobalMethodService>, IGlobalFacade
    {
        #region Constructors

        public GlobalFacade(IServiceConfiguration serviceConfiguration)
            : base(serviceConfiguration)
        {
        }

        #endregion


        #region Public Methods

        /// <summary>
        /// Gets a connection string for the specified database domain.
        /// </summary>
        /// <param name="databaseDomain">The database domain to connect to.</param>
        /// <returns>The connection string.</returns>
        public string GetConnectionString(DatabaseDomain databaseDomain)
        {
            return MethodFacade.Execute(c => c.GetConnectionString(databaseDomain));
        }

        /// <summary>
        /// Asynchronously updates the database schema.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the update on.</param>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        public async Task UpdateDatabaseAsync(DatabaseDomain databaseDomain, bool performUpdate)
        {
            await MethodFacade.ExecuteAsync(c => c.UpdateDatabase(databaseDomain, performUpdate)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously purges deleted items from the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the purge on.</param>
        public async Task<PurgeResult> PurgeDatabaseAsync(DatabaseDomain databaseDomain)
        {
            return await MethodFacade.ExecuteAsync(c => c.PurgeDatabase(databaseDomain)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously populates defaults for all entities.
        /// </summary>
        /// <param name="databaseDomain">The database domain to populate.</param>
        public async Task PopulateDataStoreAsync(DatabaseDomain databaseDomain)
        {
            await MethodFacade.ExecuteAsync(c => c.PopulateDataStore(databaseDomain)).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to retrieve.</param>
        /// <returns>The value of the global setting.</returns>
        public string GetSetting(string name)
        {
            // Get the setting by the name
            var setting = DataFacade.ExecuteQuery(uow =>
                uow.Query<Setting>().Where(s => s.Name == name)
            ).FirstOrDefault();

            // Return the value of the specified setting if it's not null
            return setting == null ? null : setting.Value;
        }

        /// <summary>
        /// Asynchronously gets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to retrieve.</param>
        /// <returns>The value of the global setting.</returns>
        public async Task<string> GetSettingAsync(string name)
        {
            // Get the setting by the name
            var setting = (await DataFacade.ExecuteQueryAsync(uow =>
                uow.Query<Setting>().Where(s => s.Name == name)
            ).ConfigureAwait(false)).FirstOrDefault();

            // Return the value of the specified setting if it's not null
            return setting == null ? null : setting.Value;
        }

        /// <summary>
        /// Gets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to retrieve.</param>
        /// <returns>The value of the global database setting.</returns>
        public string GetDatabaseSetting(DatabaseDomain databaseDomain, string name)
        {
            return GetSetting(GetDatabaseSettingName(databaseDomain, name));
        }

        /// <summary>
        /// Asynchronously gets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to retrieve.</param>
        /// <returns>The value of the global database setting.</returns>
        public async Task<string> GetDatabaseSettingAsync(DatabaseDomain databaseDomain, string name)
        {
            return await GetSettingAsync(GetDatabaseSettingName(databaseDomain, name)).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to update.</param>
        /// <param name="value">The new value to apply to the global setting.</param>
        public void SetSetting(string name, string value)
        {
            DataFacade.ExecuteUnitOfWork(uow =>
            {
                // Attempt to find an existing setting
                var setting = uow.Query<Setting>().FirstOrDefault(s => s.Name == name);

                if (setting != null)
                {
                    // An existing setting was found, so update it
                    setting.Value = value;
                }
                else
                {
                    // No existing setting was found, so create a new one
                    setting = new Setting(uow)
                    {
                        Name = name,
                        Value = value
                    };
                }
            });
        }

        /// <summary>
        /// Asynchronously sets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to update.</param>
        /// <param name="value">The new value to apply to the global setting.</param>
        public async Task SetSettingAsync(string name, string value)
        {
            await DataFacade.ExecuteUnitOfWorkAsync(uow =>
            {
                // Attempt to find an existing setting
                var setting = uow.Query<Setting>().FirstOrDefault(s => s.Name == name);

                if (setting != null)
                {
                    // An existing setting was found, so update it
                    setting.Value = value;
                }
                else
                {
                    // No existing setting was found, so create a new one
                    setting = new Setting(uow)
                    {
                        Name = name,
                        Value = value
                    };
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to update.</param>
        /// <param name="value">The new value to apply to the global database setting.</param>
        public void SetDatabaseSetting(DatabaseDomain databaseDomain, string name, string value)
        {
            SetSetting(GetDatabaseSettingName(databaseDomain, name), value);
        }

        /// <summary>
        /// Asynchronously sets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to update.</param>
        /// <param name="value">The new value to apply to the global database setting.</param>
        public async Task SetDatabaseSettingAsync(DatabaseDomain databaseDomain, string name, string value)
        {
            await SetSettingAsync(GetDatabaseSettingName(databaseDomain, name), value).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously exports a table directly from the database to an xml file.
        /// </summary>
        /// <param name="databaseDomain">The database domain to export from.</param>
        /// <param name="exportPath">
        /// The path to save the export file in.
        /// Any existing files in this folder may be overwritten.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to export.</param>
        public async Task ExportTableAsync(DatabaseDomain databaseDomain, string exportPath, string typeName)
        {
            await MethodFacade.ExecuteAsync(c => c.ExportTable(databaseDomain, exportPath, typeName)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously imports a table directly from an xml file into the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to import into.</param>
        /// <param name="importPath">
        /// The path to load the import file from.
        /// This folder should contain an xml file with the same name as the table being imported.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to import.</param>
        public async Task ImportTableAsync(DatabaseDomain databaseDomain, string importPath, string typeName)
        {
            await MethodFacade.ExecuteAsync(c => c.ImportTable(databaseDomain, importPath, typeName)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously empties tables by deleting all rows.
        /// Tables will be emptied in the order they are listed, so ensure they are ordered based on dependencies.
        /// </summary>
        /// <param name="databaseDomain">The database domain to empty tables in.</param>
        /// <param name="tableNames">An array of the table names to empty.</param>
        public async Task EmptyTablesAsync(DatabaseDomain databaseDomain, string[] tableNames)
        {
            await MethodFacade.ExecuteAsync(c => c.EmptyTables(databaseDomain, tableNames)).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously returns information about export files in the specified path.
        /// </summary>
        /// <param name="exportPath">The path to search for export files in.</param>
        /// <returns>A ListExportFilesResult object containing information about export files in the specified path.</returns>
        public async Task<ListExportFilesResult> ListExportFilesAsync(string exportPath)
        {
            return await MethodFacade.ExecuteAsync(c => c.ListExportFiles(exportPath)).ConfigureAwait(false);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Gets the name of a global database setting.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting name for.</param>
        /// <param name="name">The name of the global database setting to retrieve.</param>
        /// <returns>The full name of the global database setting.</returns>
        private static string GetDatabaseSettingName(DatabaseDomain databaseDomain, string name)
        {
            return string.Format("{0}_{1}", databaseDomain, name); ;
        }

        #endregion
    }
}
