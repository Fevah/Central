namespace Central.Engine.Models;

/// <summary>
/// Generalized icon override — works for ribbon buttons, grid columns, status indicators, device types, etc.
/// Admin sets defaults in icon_defaults, users override in user_icon_overrides.
/// Resolution: user override → admin default → code fallback.
/// </summary>
public class IconOverride
{
    public int Id { get; set; }
    public string Context { get; set; } = "";      // "ribbon", "grid.devices", "status.device", "device_type"
    public string ElementKey { get; set; } = "";    // "Save", "Active", "Core Switch"
    public string? IconName { get; set; }           // Axialist icon name
    public int? IconId { get; set; }                // icon_library FK
    public string? Color { get; set; }              // Optional colour hex
}
