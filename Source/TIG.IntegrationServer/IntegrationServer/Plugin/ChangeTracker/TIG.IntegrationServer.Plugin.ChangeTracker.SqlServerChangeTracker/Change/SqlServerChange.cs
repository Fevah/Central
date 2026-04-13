using System.Data;
using TIG.IntegrationServer.Plugin.Core.ChangeTrackerPlugin.Change;

namespace TIG.IntegrationServer.Plugin.ChangeTracker.SqlServerChangeTracker.Change
{
    public class SqlServerChange : SqlServerChangeBase
    {
        #region Constructors

        /// <summary>
        /// Constructor with data record
        /// </summary>
        /// <param name="dataRecord">Data record from presistence</param>
        public SqlServerChange(IDataRecord dataRecord)
            : base(dataRecord)
        {
        }

        #endregion
    }
}