namespace TIG.IntegrationServer.Common.MappingEntity
{
    public class RelativeQuery
    {
        public string Key { get; set; }
        public QueryDescriptor QueryDescriptor { get; set; }
        public bool Recursion { get; set; }
    }
}