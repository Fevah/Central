using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "Mapping")]
    public class Mapping
    {
        [XmlAttribute(AttributeName = "MappingType")]
        public MappingType MappingType { get; set; }

        [XmlAttribute(AttributeName = "FieldName")]
        public string FieldName { get; set; }

        [XmlAttribute(AttributeName = "Entity")]
        public string Entity { get; set; }

        [XmlAttribute(AttributeName = "Key")]
        public string Key { get; set; }

        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }

        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }
    }
}