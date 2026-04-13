using System.ServiceModel;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Shared.Contract.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum;

namespace TIG.TotalLink.Shared.Contract.Global
{
    [ServiceContract]
    public interface IGlobalMethodService : IMethodServiceBase
    {
        #region Public Methods

        /// <summary>
        /// Gets a connection string for the specified database domain.
        /// </summary>
        /// <param name="databaseDomain">The database domain to connect to.</param>
        /// <returns>The connection string.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        string GetConnectionString(DatabaseDomain databaseDomain);

        /// <summary>
        /// Updates the database schema.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the update on.</param>
        /// <param name="performUpdate">If set to false, the database will be checked to see if it requires an update, but no changes will be applied.  If set to true, the database will be updated if an update is required.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void UpdateDatabase(DatabaseDomain databaseDomain, bool performUpdate);

        /// <summary>
        /// Purges deleted items from the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to perform the purge on.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        PurgeResult PurgeDatabase(DatabaseDomain databaseDomain);

        /// <summary>
        /// Populates defaults for all entities.
        /// </summary>
        /// <param name="databaseDomain">The database domain to populate.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void PopulateDataStore(DatabaseDomain databaseDomain);

        /// <summary>
        /// Exports a table directly from the database to an xml file.
        /// </summary>
        /// <param name="databaseDomain">The database domain to export from.</param>
        /// <param name="exportPath">
        /// The path to save the export file in.
        /// Any existing files in this folder may be overwritten.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to export.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void ExportTable(DatabaseDomain databaseDomain, string exportPath, string typeName);

        /// <summary>
        /// Imports a table directly from an xml file into the database.
        /// </summary>
        /// <param name="databaseDomain">The database domain to import into.</param>
        /// <param name="importPath">
        /// The path to load the import file from.
        /// This folder should contain an xml file with the same name as the table being imported.
        /// </param>
        /// <param name="typeName">The assembly qualified name of the type to import.</param>
        [OperationContract]
        [FaultContract(typeof (ServiceFault))]
        void ImportTable(DatabaseDomain databaseDomain, string importPath, string typeName);

        /// <summary>
        /// Empties tables by deleting all rows.
        /// Tables will be emptied in the order they are listed, so ensure they are ordered based on dependencies.
        /// </summary>
        /// <param name="databaseDomain">The database domain to empty tables in.</param>
        /// <param name="tableNames">An array of the table names to empty.</param>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        void EmptyTables(DatabaseDomain databaseDomain, string[] tableNames);

        /// <summary>
        /// Returns information about export files in the specified path.
        /// </summary>
        /// <param name="exportPath">The path to search for export files in.</param>
        /// <returns>A ListExportFilesResult object containing information about export files in the specified path.</returns>
        [OperationContract]
        [FaultContract(typeof(ServiceFault))]
        ListExportFilesResult ListExportFiles(string exportPath);

        #endregion
    }
}
