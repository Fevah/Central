namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// A floor within a building. <see cref="FloorNumber"/> is signed so
/// basements use negatives (e.g. -1, -2). Free-form <see cref="FloorCode"/>
/// supports alphabetic / mezzanine schemes (G, 1, 2, 1M, B1).
/// </summary>
public class Floor : EntityBase
{
    public Guid BuildingId { get; set; }
    public Guid? FloorProfileId { get; set; }

    public string FloorCode { get; set; } = "";
    public int? FloorNumber { get; set; }
    public string? DisplayName { get; set; }
    public int? MaxRooms { get; set; }
}
