namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// A cabinet within a room. U-height governs device placement; PDU
/// references land in Phase 13.
/// </summary>
public class Rack : EntityBase
{
    public Guid RoomId { get; set; }

    public string RackCode { get; set; } = "";

    public int UHeight { get; set; } = 42;

    public string? Row { get; set; }

    public int? Position { get; set; }

    public Guid? PduAId { get; set; }
    public Guid? PduBId { get; set; }

    public int? MaxDevices { get; set; }
}
