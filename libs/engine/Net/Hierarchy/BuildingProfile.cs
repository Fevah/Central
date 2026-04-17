namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// Template governing how a new building is scaffolded. The minimal shell for
/// Phase 2 — role counts, link rules, MLAG rules, server profile reference
/// and VLAN template reference are added in later phases when those entities
/// exist.
/// </summary>
public class BuildingProfile : EntityBase
{
    public string ProfileCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DefaultFloorCount { get; set; } = 1;
}
