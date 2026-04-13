using TIG.TotalLink.Server.DataAccess.Repository.Interface;
using TIG.TotalLink.Server.DataAccess.Repository.Provider;
using TIG.TotalLink.Shared.DataModel.Core.Enum.Repository;
using TIG.TotalLink.Shared.DataModel.Repository;

namespace TIG.TotalLink.Server.DataAccess.Repository
{
    public class RepositoryDatabaseProviderFactory
    {

        /// <summary>
        /// Gets a repository database provider.
        /// </summary>
        /// <param name="repositoryInfo">Database information.</param>
        /// <returns>A Repository database provider.</returns>
        public static IRepositoryDatabaseProvider GetRepositoryDatabaseProvider(DataStore repositoryInfo)
        {
            IRepositoryDatabaseProvider repositoryDatabaseProvider = null;

            switch (repositoryInfo.DatabaseProvider)
            {
                case DatabaseProvider.MSSqlServer:
                    repositoryDatabaseProvider = new SqlServerRepositoryDatabaseProvider(repositoryInfo.Server,
                        repositoryInfo.IntegratedSecurity, repositoryInfo.UserName, repositoryInfo.Password);
                    break;
            }

            return repositoryDatabaseProvider;
        }
    }
}