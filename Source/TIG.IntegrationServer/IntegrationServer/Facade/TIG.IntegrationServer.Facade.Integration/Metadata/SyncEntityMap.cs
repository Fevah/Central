using Newtonsoft.Json;
using TIG.IntegrationServer.Common.MappingEntity;
using TIG.IntegrationServer.Common.Provider;

namespace TIG.TotalLink.Shared.DataModel.Integration
{
    public partial class SyncEntityMap
    {
        private SyncKeyInfo _syncKeyInfo;
        private SyncMapping _syncMapping;

        public SyncKeyInfo GetSyncKeyInfo()
        {
            return _syncKeyInfo ?? (_syncKeyInfo = JsonConvert.DeserializeObject<SyncKeyInfo>(SyncKeyInfo));
        }

        public SyncMapping GetSyncMapping()
        {
            return _syncMapping ?? (_syncMapping = XmlSerializeProvider.Deserialize<SyncMapping>(FieldMappings));
        }
    }
}