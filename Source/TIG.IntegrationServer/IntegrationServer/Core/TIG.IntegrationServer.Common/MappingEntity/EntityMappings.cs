using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "EntityMappings")]
    public class EntityMappings
    {
        [XmlElement(ElementName = "EntityMapping")]
        public List<EntityMapping> EntityMapping { get; set; }

        [XmlAttribute("Action")]
        public Action Action { get; set; }
    }
}