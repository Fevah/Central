using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevExpress.Data.Filtering;
using DevExpress.Xpo.DB;
using DevExpress.Xpo.DB.Helpers;
using DevExpress.Xpo.Helpers;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.DataAccess.Repository;
using TIG.TotalLink.Server.DataAccess.Repository.DataModel;
using TIG.TotalLink.Shared.DataModel.Core.Enum;
using TIG.TotalLink.Shared.DataModel.Repository;

namespace TIG.TotalLink.Server.DataService.Repository
{
    public class RepositoryDataService : DataServiceBase
    {
        #region Private Fields

        private const string DataStoreTableName = "DataStore";

        #endregion

        #region Constructors

        public RepositoryDataService()
            : base(CreateDataStore())
        {
        }

        #endregion


        #region Static Methods

        /// <summary>
        /// Creates a data store for this service.
        /// </summary>
        /// <returns>A DataCacheRoot.</returns>
        private static ICachedDataStore CreateDataStore()
        {
            // If the DataCacheRoot has already been created, return the existing one
            if (CacheRoot != null)
                return CacheRoot;

            // Create a database data store
            var dataStore = CreateDatabaseDataStore(DatabaseDomain.Repository);

            // Create and return a DataCacheRoot
            return CreateCacheRoot(dataStore);
        }

        #endregion


        #region Overrides

        /// <summary>
        /// Modify Data Cached method.
        /// </summary>
        /// <param name="cookie">Data cache cookie.</param>
        /// <param name="dmlStatements">modification statements.</param>
        /// <returns>Modification result.</returns>
        public override OperationResult<DataCacheModificationResult> ModifyDataCached(DataCacheCookie cookie, ModificationStatement[] dmlStatements)
        {
            // Get properties information of DBStorageStats
            var dataStatsPropertis = typeof(DBStorageStats).GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(p => p.Name);

            // Handle repository store database when create or delete repository info.
            foreach (var modificationStatement in dmlStatements.Where(p => p.TableName == DataStoreTableName))
            {
                // Delete fields and values of database stats
                var parameters = new List<OperandValue>();
                var operands = modificationStatement.Operands.OfType<QueryOperand>().ToArray();

                for (var i = 0; i < operands.Length; i++)
                {
                    var operand = operands[i];
                    var dataStatsOperand = dataStatsPropertis.FirstOrDefault(f => f.Key == operand.ColumnName);

                    if (dataStatsOperand.Value != null)
                    {
                        modificationStatement.Operands.Remove(operand);
                        continue;
                    }

                    var value = modificationStatement.Parameters[i];
                    parameters.Add(value);
                }

                modificationStatement.Parameters = new QueryParameterCollection(parameters.ToArray());
            }

            return base.ModifyDataCached(cookie, dmlStatements);
        }

        /// <summary>
        /// Warp select data cached.
        /// </summary>
        /// <param name="cookie">Data cache cookie</param>
        /// <param name="selects">Select statement</param>
        /// <returns>Select data result</returns>
        public override OperationResult<DataCacheWarpSelectDataResult> WarpSelectDataCached(DataCacheCookie cookie, SelectStatement[] selects)
        {
            // Get properties information of DBStorageStats
            var dataStatsPropertis = typeof(DBStorageStats).GetProperties(BindingFlags.Instance | BindingFlags.Public).ToDictionary(p => p.Name);
            var dataStatsFieldsForSelect = new List<List<PropertyInfo>>();

            // Remove db storagestas fields from query operand, and cache them in local.
            foreach (var select in selects.Where(p => p.TableName == DataStoreTableName))
            {
                var dataStatsOperands = select.Operands.OfType<QueryOperand>().Where(p => dataStatsPropertis.Keys.Any(f => f == p.ColumnName)).ToList();
                var orderedDataStatsFields = new List<PropertyInfo>();

                foreach (var dataStatsQperand in dataStatsOperands)
                {
                    orderedDataStatsFields.Add(dataStatsPropertis[dataStatsQperand.ColumnName]);
                    select.Operands.Remove(dataStatsQperand);
                }

                if (orderedDataStatsFields.Any())
                    dataStatsFieldsForSelect.Add(orderedDataStatsFields);
            }

            // Get select result from base.
            var warpSelectData = base.WarpSelectDataCached(cookie, selects);

            if (!dataStatsFieldsForSelect.Any())
                return warpSelectData;

            // Unwarp result to repository information.
            var selectData = WcfUsedAsDumbPipeHelper.Unwarp(warpSelectData.Result.SelectResult);

            // Rebuild result row plus database stats information.
            var index = 0;
            foreach (var select in selects.Where(p => p.TableName == DataStoreTableName))
            {
                var orderedDataStatsFields = dataStatsFieldsForSelect[index];

                foreach (var selectStatementResultRow in selectData[index].Rows)
                {
                    // Build query entity.
                    var repositoryInfo = BuildQueryEntity<DataStore>(selectStatementResultRow.Values, @select.Operands.OfType<QueryOperand>());

                    // Get data stats inforamtion from repositoryinfo.
                    var dbStatus = GetDatabaseStorageStats(repositoryInfo);

                    var values = new List<object>();
                    // Get repository information from base result.
                    for (var i = 0; i < selectStatementResultRow.Values.Length - 2; i++)
                    {
                        var value = selectStatementResultRow.Values[i];
                        values.Add(value);
                    }

                    // Add db status information to new values.
                    values.AddRange(orderedDataStatsFields.Select(orderedDataStatsField => orderedDataStatsField.GetValue(dbStatus)));

                    // Add GC information from base result.
                    for (var i = selectStatementResultRow.Values.Length - 2; i < selectStatementResultRow.Values.Length; i++)
                    {
                        var value = selectStatementResultRow.Values[i];
                        values.Add(value);
                    }

                    selectStatementResultRow.Values = values.ToArray();
                    selectStatementResultRow.XmlValues = values.ToArray();
                }
                index++;
            }

            warpSelectData.Result.SelectResult = WcfUsedAsDumbPipeHelper.Warp(selectData);

            return warpSelectData;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Get database strage stats
        /// </summary>
        /// <param name="repositoryInfo">Database information</param>
        /// <returns></returns>
        private DBStorageStats GetDatabaseStorageStats(DataStore repositoryInfo)
        {
            var repositoryDatabaseProvider = RepositoryDatabaseProviderFactory.GetRepositoryDatabaseProvider(repositoryInfo);
            return repositoryDatabaseProvider.GetDatabaseStats(repositoryInfo.DatabaseName, repositoryInfo.DataFileSizeLimit);
        }

        #endregion
    }
}
