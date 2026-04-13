using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "Mappings")]
    public class Mappings
    {
        [XmlElement(ElementName = "Mapping")]
        public List<Mapping> Mapping { get; set; }
    }
}