using System;
using TIG.IntegrationServer.SyncEngine.Core.Interface;

namespace TIG.TotalLink.Shared.DataModel.Integration
{
    public class SyncEntityStatus : ISyncEntityStatus
    {
        public string EntityKey { get; set; }
        public string SourceAgentId { get; set; }
        public string TargetAgentId { get; set; }
        public DateTime SynceTime { get; set; }
    }
}