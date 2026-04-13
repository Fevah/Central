using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "EntityRelationship")]
    public class EntityRelationship
    {
        [XmlElement(ElementName = "Mappings")]
        public List<Mappings> Mappings { get; set; }
        [XmlAttribute(AttributeName = "Entity")]
        public string Entity { get; set; }
        [XmlAttribute(AttributeName = "Key")]
        public string Key { get; set; }
    }
}