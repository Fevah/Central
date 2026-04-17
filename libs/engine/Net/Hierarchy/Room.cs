namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// A room within a floor (MDF / IDF / Data Hall / Office / etc.). Holds racks.
/// Environmental and power-feed links are wired in Phase 13.
/// </summary>
public class Room : EntityBase
{
    public Guid FloorId { get; set; }

    public string RoomCode { get; set; } = "";

    /// <summary>MDF / IDF / DataHall / Office / Comms / Plant / Custom.</summary>
    public string RoomType { get; set; } = "DataHall";

    public int? MaxRacks { get; set; }

    public string? EnvironmentalNotes { get; set; }

    public Guid? PowerFeedAId { get; set; }
    public Guid? PowerFeedBId { get; set; }
}
