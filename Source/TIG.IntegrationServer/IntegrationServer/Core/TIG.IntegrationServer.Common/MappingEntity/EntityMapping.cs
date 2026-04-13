using System.Collections.Generic;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    [XmlRoot(ElementName = "EntityMapping")]
    public class EntityMapping
    {
        [XmlElement(ElementName = "FieldMapping")]
        public List<FieldMapping> FieldMapping { get; set; }
        [XmlAttribute(AttributeName = "Name")]
        public string Name { get; set; }
        [XmlAttribute(AttributeName = "Filter")]
        public string Filter { get; set; }
        [XmlAttribute("Action")]
        public Action Action { get; set; }
        [XmlAttribute(AttributeName = "RelativeQuery")]
        public string RelativeQueryString { get; set; }
        [XmlAttribute(AttributeName = "Id")]
        public string Id { get; set; }
        [XmlAttribute(AttributeName = "IdentityKey")]
        public string IdentityKey { get; set; }
        [XmlIgnore]
        public RelativeQuery RelativeQuery
        {
            get
            {
                return string.IsNullOrWhiteSpace(RelativeQueryString) ? null
                    : JsonConvert.DeserializeObject<RelativeQuery>(RelativeQueryString);
            }
        }
    }
}