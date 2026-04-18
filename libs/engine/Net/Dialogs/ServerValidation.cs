using Central.Engine.Net.Servers;

namespace Central.Engine.Net.Dialogs;

/// <summary>
/// Pure validation rules for the server detail flow — matches the
/// Hierarchy/Pool/Allocation validators in shape. Tested without a
/// WPF Dispatcher; the eventual ServerDetailDialog (Phase 6f) calls
/// these to avoid duplicating rule logic inline.
/// </summary>
public static class ServerValidation
{
    public enum Mode { New, Edit }

    public static IReadOnlyList<string> ValidateProfile(ServerProfile p)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(p.ProfileCode))    errors.Add("Code is required.");
        if (string.IsNullOrWhiteSpace(p.DisplayName))    errors.Add("Display name is required.");
        if (p.NicCount < 1)                              errors.Add("NIC count must be >= 1.");
        if (p.DefaultLoopbackPrefix is < 1 or > 128)     errors.Add("Loopback prefix must be 1..128.");
        if (string.IsNullOrWhiteSpace(p.NamingTemplate)) errors.Add("Naming template is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateServer(Server s, Mode mode)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(s.Hostname))       errors.Add("Hostname is required.");
        if (mode == Mode.New && s.BuildingId is null)
            errors.Add("Building is required.");
        if (mode == Mode.New && s.ServerProfileId is null)
            errors.Add("Server profile is required.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateNic(ServerNic n, Mode mode)
    {
        var errors = new List<string>();
        if (n.NicIndex < 0)
            errors.Add("NIC index must be >= 0.");
        if (mode == Mode.New && n.ServerId == Guid.Empty)
            errors.Add("Server is required.");
        return errors;
    }
}
