using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Dialogs;

/// <summary>
/// Pure validation rules for the hierarchy-detail dialog in the WPF
/// Networking module. Lives in the engine assembly rather than inside
/// <c>HierarchyDetailDialog.xaml.cs</c> so the rules are testable
/// without a <c>Dispatcher</c> / XAML instantiation.
///
/// <para>Each <c>Validate*</c> method returns the list of user-facing
/// error messages. An empty list means "OK". The dialog stops and
/// shows the first message in a <c>DXMessageBox</c>.</para>
///
/// <para><c>Mode</c> toggles "New mode requires parent FK" rules —
/// in Edit mode the parent FK is already set and immutable.</para>
/// </summary>
public static class HierarchyValidation
{
    public enum Mode { New, Edit }

    public static IReadOnlyList<string> ValidateRegion(Region r)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.RegionCode))   errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(r.DisplayName))  errors.Add("Display name is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateSite(Site s, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.SiteCode))     errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(s.DisplayName))  errors.Add("Display name is required.");
        if (mode == Mode.New && s.RegionId == Guid.Empty)
            errors.Add("Region is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateBuilding(Building b, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(b.BuildingCode)) errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(b.DisplayName))  errors.Add("Display name is required.");
        if (mode == Mode.New && b.SiteId == Guid.Empty)
            errors.Add("Site is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateFloor(Floor f, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(f.FloorCode))    errors.Add("Code is required.");
        if (mode == Mode.New && f.BuildingId == Guid.Empty)
            errors.Add("Building is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateRoom(Room r, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(r.RoomCode))     errors.Add("Code is required.");
        if (mode == Mode.New && r.FloorId == Guid.Empty)
            errors.Add("Floor is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateRack(Rack k, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(k.RackCode))     errors.Add("Code is required.");
        if (mode == Mode.New && k.RoomId == Guid.Empty)
            errors.Add("Room is required.");
        return errors;
    }
}
