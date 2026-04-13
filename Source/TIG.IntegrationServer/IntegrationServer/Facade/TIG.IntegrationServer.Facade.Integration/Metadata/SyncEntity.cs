using Newtonsoft.Json;

namespace TIG.TotalLink.Shared.DataModel.Integration
{
    public partial class SyncEntity
    {
        private PrimaryKeyInfo _primaryInfo;

        public Field GetDatabasePrimaryKey()
        {
            var primaryKey = GetPrimaryKeyInfo();
            return primaryKey == null ? null : primaryKey.DbKey;
        }

        public Field GetODataKey()
        {
            var primaryKey = GetPrimaryKeyInfo();
            return primaryKey == null ? null : primaryKey.ODataKey;
        }

        private PrimaryKeyInfo GetPrimaryKeyInfo()
        {
            if (PrimaryKey == null)
            {
                return null;
            }

            return _primaryInfo ?? (_primaryInfo = JsonConvert.DeserializeObject<PrimaryKeyInfo>(PrimaryKey));
        }
    }
}