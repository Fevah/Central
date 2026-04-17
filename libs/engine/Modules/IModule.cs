namespace Central.Engine.Modules;

/// <summary>
/// Base contract for all Central modules.
/// Each module is an Autofac.Module that also implements this interface
/// to provide metadata for the shell.
/// </summary>
public interface IModule
{
    /// <summary>Display name — "Devices", "Links", "Routing"</summary>
    string Name { get; }

    /// <summary>Permission category — "devices", "links", "bgp"</summary>
    string PermissionCategory { get; }

    /// <summary>Ribbon tab sort order (10, 20, 30...)</summary>
    int SortOrder { get; }
}
