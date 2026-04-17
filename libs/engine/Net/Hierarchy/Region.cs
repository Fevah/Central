namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// A geographic region within an organisation (e.g. UK, US, EU, EXP).
/// Contains sites. Holds default IP and ASN pools for its scope.
/// </summary>
public class Region : EntityBase
{
    /// <summary>Short code unique within the organisation. E.g. "UK".</summary>
    public string RegionCode { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>Optional default IP pool for this region. Wired in Phase 3.</summary>
    public Guid? DefaultIpPoolId { get; set; }

    /// <summary>Optional default ASN pool for this region. Wired in Phase 3.</summary>
    public Guid? DefaultAsnPoolId { get; set; }

    /// <summary>FullMesh / Hub&amp;Spoke / None.</summary>
    public string B2bMeshPolicy { get; set; } = "None";
}
