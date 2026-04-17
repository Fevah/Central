using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// Capacity rule: buildings created from <see cref="BuildingProfileId"/>
/// are expected to contain <see cref="ExpectedCount"/> devices of role
/// <see cref="DeviceRoleId"/>. Feeds the capacity-planning / validation
/// views in later phases — this row itself doesn't enforce anything,
/// it's pure advisory data.
/// </summary>
public class BuildingProfileRoleCount : EntityBase
{
    public Guid BuildingProfileId { get; set; }
    public Guid DeviceRoleId { get; set; }
    public int ExpectedCount { get; set; }
}
