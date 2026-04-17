namespace Central.Module.Networking.Pools;

/// <summary>
/// Flat row bound to the DevExpress TreeListControl in
/// <c>PoolsTreePanel</c>. Mirrors the <c>HierarchyNode</c> shape —
/// synthetic string <see cref="Id"/> and <see cref="ParentId"/> so
/// pools, blocks, subnets, allocations of different underlying types
/// can share a tree without Guid collisions.
///
/// Utilisation is computed at load time — it's a snapshot, not a live
/// projection. Refreshing the panel re-queries and recomputes.
/// </summary>
public class PoolTreeNode
{
    public string Id { get; set; } = "";
    public string? ParentId { get; set; }

    public Guid EntityId { get; set; }
    public string NodeType { get; set; } = "";   // AsnPool / AsnBlock / IpPool / Subnet / VlanPool / VlanBlock / MlagPool / MstpRule

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Display range — "65100-65110" for ASN, "10.0.0.0/24" for IP.</summary>
    public string Range { get; set; } = "";

    /// <summary>Number of children / allocations used. 0 when the capacity isn't enumerable.</summary>
    public long Used { get; set; }

    /// <summary>Total capacity. 0 when not applicable (e.g. MSTP rules).</summary>
    public long Capacity { get; set; }

    /// <summary>
    /// Utilisation in [0, 1]. The grid renders it through a
    /// ProgressBarEdit column. Returns 0 for rows without a capacity
    /// so the bar stays flat rather than showing NaN.
    /// </summary>
    public double UtilisationPct => Capacity > 0 ? (double)Used / Capacity : 0;

    /// <summary>Pre-formatted "42 / 100" for the grid's text column.</summary>
    public string UtilisationText => Capacity > 0 ? $"{Used} / {Capacity}" : "";

    public string Status { get; set; } = "";
    public string Lock { get; set; } = "";
    public int Version { get; set; }
}
