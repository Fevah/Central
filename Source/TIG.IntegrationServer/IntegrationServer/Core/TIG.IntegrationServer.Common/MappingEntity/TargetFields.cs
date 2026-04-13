using System.Collections.Generic;
using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "TargetFields")]
    public class TargetFields
    {
        [XmlElement(ElementName = "Field")]
        public List<Field> Field { get; set; }
    }
}