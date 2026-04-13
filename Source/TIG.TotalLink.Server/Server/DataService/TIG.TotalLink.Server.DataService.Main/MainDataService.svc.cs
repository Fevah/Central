using DevExpress.Xpo.DB;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Shared.DataModel.Core.Enum;

namespace TIG.TotalLink.Server.DataService.Main
{
    public class MainDataService : DataServiceBase
    {
        #region Constructors

        public MainDataService()
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
            var dataStore = CreateDatabaseDataStore(DatabaseDomain.Main);

            // Create and return a DataCacheRoot
            return CreateCacheRoot(dataStore);
        }

        #endregion
    }
}
