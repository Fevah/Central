using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "SourceFields")]
    public class SourceFields
    {
        [XmlElement(ElementName = "Field")]
        public List<Field> Field { get; set; }
    }
}