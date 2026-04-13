using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "Enum")]
    public class Enum
    {
        [XmlElement(ElementName = "Field")]
        public List<Field> Field { get; set; }
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }
    }
}