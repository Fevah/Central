using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "SyncMapping")]
    public class SyncMapping
    {
        [XmlElement(ElementName = "EntityMappings")]
        public EntityMappings EntityMappings { get; set; }

        [XmlElement(ElementName = "Enums")]
        public Enums Enums { get; set; }

        [XmlElement(ElementName = "EntityRelationship")]
        public EntityRelationship EntityRelationship { get; set; }
    }
}