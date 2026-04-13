using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "Enums")]
    public class Enums
    {
        [XmlElement(ElementName = "Enum")]
        public List<Enum> Enum { get; set; }
    }
}