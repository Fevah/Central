using System.Threading.Tasks;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Shared.Contract.Global;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.Facade.Core;

namespace TIG.TotalLink.Shared.Facade.Global
{
    public interface IGlobalFacade : IFacadeBase
    {
        #region Public Methods

        /// <summary>
        /// Gets a connection string for the specified database domain.
        /// </summary>
        /// <param name="databaseDomain">The database domain to connect to.</param>
        /// <returns>The connection string.</returns>
        string GetConnectionString(DatabaseDomain databaseDomain);

        /// <summary>
        /// Asynchronously updates the database schema.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the update on.</param>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        Task UpdateDatabaseAsync(DatabaseDomain databaseDomain, bool performUpdate);

        /// <summary>
        /// Asynchronously purges deleted items from the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the purge on.</param>
        Task<PurgeResult> PurgeDatabaseAsync(DatabaseDomain databaseDomain);

        /// <summary>
        /// Asynchronously populates defaults for all entities.
        /// </summary>
        /// <param name="databaseDomain">The database domain to populate.</param>
        Task PopulateDataStoreAsync(DatabaseDomain databaseDomain);

        /// <summary>
        /// Gets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to retrieve.</param>
        /// <returns>The value of the global setting.</returns>
        string GetSetting(string name);

        /// <summary>
        /// Asynchronously gets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to retrieve.</param>
        /// <returns>The value of the global setting.</returns>
        Task<string> GetSettingAsync(string name);

        /// <summary>
        /// Gets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to retrieve.</param>
        /// <returns>The value of the global database setting.</returns>
        string GetDatabaseSetting(DatabaseDomain databaseDomain, string name);

        /// <summary>
        /// Asynchronously gets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to retrieve.</param>
        /// <returns>The value of the global database setting.</returns>
        Task<string> GetDatabaseSettingAsync(DatabaseDomain databaseDomain, string name);

        /// <summary>
        /// Sets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to update.</param>
        /// <param name="value">The new value to apply to the global setting.</param>
        void SetSetting(string name, string value);

        /// <summary>
        /// Asynchronously sets the value of a global setting by name.
        /// </summary>
        /// <param name="name">The name of the global setting to update.</param>
        /// <param name="value">The new value to apply to the global setting.</param>
        Task SetSettingAsync(string name, string value);

        /// <summary>
        /// Sets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to update.</param>
        /// <param name="value">The new value to apply to the global database setting.</param>
        void SetDatabaseSetting(DatabaseDomain databaseDomain, string name, string value);

        /// <summary>
        /// Asynchronously sets the value of a global database setting by name.
        /// </summary>
        /// <param name="databaseDomain">The database domain to retrieve the setting for.</param>
        /// <param name="name">The name of the global database setting to update.</param>
        /// <param name="value">The new value to apply to the global database setting.</param>
        Task SetDatabaseSettingAsync(DatabaseDomain databaseDomain, string name, string value);

        /// <summary>
        /// Asynchronously exports a table directly from the database to an xml file.
        /// </summary>
        /// <param name="databaseDomain">The database domain to export from.</param>
        /// <param name="exportPath">
        /// The path to save the export file in.
        /// Any existing files in this folder may be overwritten.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to export.</param>
        Task ExportTableAsync(DatabaseDomain databaseDomain, string exportPath, string typeName);

        /// <summary>
        /// Asynchronously imports a table directly from an xml file into the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to import into.</param>
        /// <param name="importPath">
        /// The path to load the import file from.
        /// This folder should contain an xml file with the same name as the table being imported.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to import.</param>
        Task ImportTableAsync(DatabaseDomain databaseDomain, string importPath, string typeName);

        /// <summary>
        /// Asynchronously empties tables by deleting all rows.
        /// Tables will be emptied in the order they are listed, so ensure they are ordered based on dependencies.
        /// </summary>
        /// <param name="databaseDomain">The database domain to empty tables in.</param>
        /// <param name="tableNames">An array of the table names to empty.</param>
        Task EmptyTablesAsync(DatabaseDomain databaseDomain, string[] tableNames);

        /// <summary>
        /// Returns information about export files in the specified path.
        /// </summary>
        /// <param name="exportPath">The path to search for export files in.</param>
        /// <returns>A ListExportFilesResult object containing information about export files in the specified path.</returns>
        Task<ListExportFilesResult> ListExportFilesAsync(string exportPath);

        #endregion
    }
}
