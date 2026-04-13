namespace TIG.IntegrationServer.Common.MappingEntity
{
    public class QueryDescriptor
    {
        //RelativeQuery="{Key: 'Oid', QueryDescriptor: {Entity: 'CustomerLink', Query: 'Target/Oid', Type: 'System.Guid', Target: {Property: 'Source', Filter: '[CustomerType.Name]=Branch'}, Expand: ['Source/CustomerType', 'Target']}, Recursion: true}">
        public string Entity { get; set; }
        public string Query { get; set; }
        public string[] Expands { get; set; }
        public EntityFilterDescriptor Target { get; set; }
        public string Type { get; set; }
    }
}