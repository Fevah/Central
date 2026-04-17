namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// Template for creating sites. Governs default max-buildings,
/// default building profile, default floor count.
/// </summary>
public class SiteProfile : EntityBase
{
    /// <summary>Short code unique within the organisation.</summary>
    public string ProfileCode { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public int DefaultMaxBuildings { get; set; } = 12;

    public Guid? DefaultBuildingProfileId { get; set; }

    public int DefaultFloorsPerBuilding { get; set; } = 1;

    public bool AllowMixedBuildingProfiles { get; set; } = true;
}
