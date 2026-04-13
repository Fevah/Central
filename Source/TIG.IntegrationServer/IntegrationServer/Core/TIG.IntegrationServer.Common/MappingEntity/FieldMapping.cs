using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "FieldMapping")]
    public class FieldMapping
    {
        [XmlElement(ElementName = "SourceFields")]
        public SourceFields SourceFields { get; set; }

        [XmlElement(ElementName = "TargetFields")]
        public TargetFields TargetFields { get; set; }
    }
}