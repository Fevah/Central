using System.Xml.Serialization;

namespace TIG.IntegrationServer.Common.MappingEntity
{
    public enum Action
    {
        [XmlEnum(Name = "Default")]
        Default,
        [XmlEnum(Name = "All")]
        All,
        [XmlEnum(Name = "Update")]
        Update,
        [XmlEnum(Name = "Combine")]
        Combine,
        [XmlEnum(Name = "Separate")]
        Separate,
    }
}