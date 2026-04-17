namespace Central.Core.Models;

public class RolePermission
{
    public string Module    { get; set; } = "";
    public bool   CanView   { get; set; }
    public bool   CanEdit   { get; set; }
    public bool   CanDelete      { get; set; }
    public bool   CanViewReserved { get; set; } = true;
}
