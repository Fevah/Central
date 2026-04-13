using System;
using System.IO;
using System.Linq;
using DevExpress.DataAccess.Native.Sql;
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Shared.DataModel.Global;

namespace TIG.TotalLink.Server.DataService.Global
{
    public class GlobalDataService : DataServiceBase
    {
        #region Constructors

        public GlobalDataService()
            : base(CreateDataStore())
        {
            PopulateDataStore();
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

            // Create an XmlDataSet data store
            var settingsFilename = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GlobalSettings.xml");
            var dataStore = XpoDefault.GetConnectionProvider(string.Format("XpoProvider=XmlDataSet;Data Source={0}", settingsFilename), AutoCreateOption.DatabaseAndSchema);

            // Create and return a DataCacheRoot
            return CreateCacheRoot(dataStore);
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Populates the data store with default data.
        /// </summary>
        private void PopulateDataStore()
        {
            ExecuteLocalUnitOfWork<XpoProvider>(uow =>
            {
                // Only attempt to populate the providers if there aren't any yet
                if (!uow.Query<XpoProvider>().Any())
                {
                    // Add all available XpoProviders to the data store
                    var providerFactories = DataConnectionHelper.GetProviderFactories();
                    foreach (var providerFactory in providerFactories)
                    {
                        new XpoProvider(uow)
                        {
                            Name = providerFactory.ProviderKey,
                            FileFilter = providerFactory.FileFilter,
                            HasIntegratedSecurity = providerFactory.HasIntegratedSecurity,
                            HasMultipleDatabases = providerFactory.HasMultipleDatabases,
                            HasPassword = providerFactory.HasPassword,
                            HasUserName = providerFactory.HasUserName,
                            IsFileBased = providerFactory.IsFilebased,
                            IsServerBased = providerFactory.IsServerbased,
                            MeanSchemaGeneration = providerFactory.MeanSchemaGeneration,
                            SupportStoredProcedures = providerFactory.SupportStoredProcedures
                        };
                    }
                }
            });

            // Call the Populate method in the data model
            var populate = new Populate();
            populate.PopulateDataStore(LocalDataLayer);
        }

        #endregion
    }
}
