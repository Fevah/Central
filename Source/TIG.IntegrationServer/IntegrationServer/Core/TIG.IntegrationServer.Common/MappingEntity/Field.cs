using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "Field")]
    public class Field
    {
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }
        [XmlAttribute(AttributeName = "Value")]
        public string Value { get; set; }
        [XmlAttribute(AttributeName = "Type")]
        public string Type { get; set; }
        [XmlAttribute(AttributeName = "Length")]
        public int Length { get; set; }
        [XmlAttribute(AttributeName = "Convert")]
        public string Convert { get; set; }
    }
}