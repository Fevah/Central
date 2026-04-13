using DevExpress.Xpo.DB;
using TIG.TotalLink.Server.Core;
using TIG.TotalLink.Server.DataService.ActiveDirectory.Provider;

namespace TIG.TotalLink.Server.DataService.ActiveDirectory
{
    public class ActiveDirectoryDataService : DataServiceBase
    {
        #region Constructors

        public ActiveDirectoryDataService()
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
            if (CacheRoot != null)
                return CacheRoot;

            var dataStore = new ActiveDirectoryProvider();

            return CreateCacheRoot(dataStore);
        }

        #endregion
    }
}
