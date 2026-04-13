using System.Collections.Generic;

namespace TIG.TotalLink.Shared.DataModel.Integration
{
    public class SyncKeyInfo
    {
        public Field Source { get; set; }
        public Field Target { get; set; }
        public List<Field> TargetIdentities { get; set; }
    }
}