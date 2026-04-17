namespace Central.Engine.Net.Hierarchy;

public class FloorProfile : EntityBase
{
    public string ProfileCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DefaultRoomCount { get; set; } = 1;
    public int DefaultRackCountPerRoom { get; set; } = 10;
}
