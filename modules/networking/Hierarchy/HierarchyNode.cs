namespace Central.Module.Networking.Hierarchy;

/// <summary>
/// Flat row bound to the DevExpress TreeListControl. The control builds the
/// tree from <see cref="Id"/> / <see cref="ParentId"/> so we never nest
/// collections ourselves — one flat list per render.
///
/// <see cref="EntityId"/> is the net.*.id (Guid); <see cref="Id"/> is a
/// synthetic composite key ("Region:{guid}", "Site:{guid}" etc.) because
/// TreeListControl needs a single comparable key type across every level
/// and two siblings at different levels could otherwise share a Guid.
/// </summary>
public class HierarchyNode
{
    public string Id { get; set; } = "";
    public string? ParentId { get; set; }

    public Guid EntityId { get; set; }
    public string NodeType { get; set; } = "";     // Region / Site / Building / Floor / Room / Rack

    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Lock { get; set; } = "";
    public int Version { get; set; }
}
