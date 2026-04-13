using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    public enum MappingType
    {
        [XmlEnum(Name = "Link")]
        Link,
        [XmlEnum(Name = "Entity")]
        Entity
    }
}