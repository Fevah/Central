namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// A physical geographic location (campus) that can contain multiple
/// buildings. E.g. Milton Park is a site; MEP-91..96 are buildings within it.
/// </summary>
public class Site : EntityBase
{
    public Guid RegionId { get; set; }
    public Guid? SiteProfileId { get; set; }

    /// <summary>Short code unique within the region (e.g. "MP").</summary>
    public string SiteCode { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
    public string? City { get; set; }
    public string? StateOrCounty { get; set; }
    public string? Postcode { get; set; }
    public string? Country { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    /// <summary>IANA timezone.</summary>
    public string? Timezone { get; set; }

    public int? PrimaryContactUserId { get; set; }

    public int? MaxBuildings { get; set; }
}
