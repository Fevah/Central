using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

/// <summary>
/// Common contract for all network link types (P2P, B2B, FW).
/// Enables generic handling in LinkEditorHelper and config generation.
/// </summary>
public interface INetworkLink : INotifyPropertyChanged
{
    int Id { get; set; }
    string LinkId { get; set; }
    string Vlan { get; set; }
    string Status { get; set; }
    string Subnet { get; set; }
    string DeviceA { get; set; }
    string DeviceB { get; set; }

    /// <summary>PicOS config for side A.</summary>
    string ConfigA { get; }
    /// <summary>PicOS config for side B.</summary>
    string ConfigB { get; }

    /// <summary>Build PicOS set commands for one side of the link.</summary>
    List<string> BuildConfig(bool sideA);

    /// <summary>Validate link completeness. Returns list of warnings (empty = valid).</summary>
    List<string> Validate();
}

/// <summary>
/// Base class for network link models. Provides INotifyPropertyChanged
/// and shared PicOS config generation helpers.
/// </summary>
public abstract class NetworkLinkBase : INetworkLink
{
    // ── Abstract — subclass provides these ─────────────────────────────

    public abstract int Id { get; set; }
    public abstract string LinkId { get; set; }
    public abstract string Vlan { get; set; }
    public abstract string Status { get; set; }
    public abstract string Subnet { get; set; }
    public abstract string DeviceA { get; set; }
    public abstract string DeviceB { get; set; }

    public string ConfigA => string.Join("\n", BuildConfig(sideA: true));
    public string ConfigB => string.Join("\n", BuildConfig(sideA: false));
    public abstract List<string> BuildConfig(bool sideA);

    /// <summary>Validate link fields. Override for type-specific rules.</summary>
    public virtual List<string> Validate()
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(DeviceA)) warnings.Add("Device A is empty");
        if (string.IsNullOrWhiteSpace(DeviceB)) warnings.Add("Device B is empty");
        if (string.IsNullOrWhiteSpace(Vlan)) warnings.Add("VLAN is not set");
        if (string.IsNullOrWhiteSpace(Subnet)) warnings.Add("Subnet is not set");
        return warnings;
    }

    /// <summary>True if the link has all required fields for config generation.</summary>
    public bool IsComplete => Validate().Count == 0;

    /// <summary>Validation status icon for grid display.</summary>
    public string ValidationIcon => IsComplete ? "✓" : "⚠";

    /// <summary>Validation status color.</summary>
    public string ValidationColor => IsComplete ? "#22C55E" : "#F59E0B";

    /// <summary>Tooltip showing validation warnings.</summary>
    public string ValidationTooltip => IsComplete ? "Ready to deploy" : string.Join("\n", Validate());

    /// <summary>Detail config lines for master-detail expansion. Shows generated PicOS commands per side.</summary>
    public System.Collections.ObjectModel.ObservableCollection<LinkConfigLine> DetailConfigLines { get; } = new();

    /// <summary>Populate DetailConfigLines from BuildConfig. Called on master row expand.</summary>
    public void GenerateDetailConfig()
    {
        DetailConfigLines.Clear();
        foreach (var line in BuildConfig(sideA: true))
            DetailConfigLines.Add(new LinkConfigLine { Side = DeviceA, Command = line });
        foreach (var line in BuildConfig(sideA: false))
            DetailConfigLines.Add(new LinkConfigLine { Side = DeviceB, Command = line });
    }

    // ── Shared config helpers ──────────────────────────────────────────

    /// <summary>Generate VLAN + L3 interface set commands (common to all link types).</summary>
    protected void AddVlanAndL3(List<string> cmds, string vlan, string vlanDesc, string l3Desc, string? ip, string subnet)
    {
        cmds.Add($"set vlans vlan-id {vlan} description \"{vlanDesc}\"");
        cmds.Add($"set vlans vlan-id {vlan} l3-interface \"vlan-{vlan}\"");
        cmds.Add($"set l3-interface vlan-interface vlan-{vlan} description \"{l3Desc}\"");
        if (!string.IsNullOrEmpty(ip))
        {
            var prefix = LinkHelper.ExtractPrefix(subnet);
            cmds.Add($"set l3-interface vlan-interface vlan-{vlan} address {ip} prefix-length {prefix}");
        }
    }

    /// <summary>Generate physical port set commands (common to all link types).</summary>
    protected void AddPort(List<string> cmds, string vlan, string port, string desc)
    {
        if (string.IsNullOrEmpty(port)) return;
        cmds.Add($"set interface gigabit-ethernet {port} description \"{desc}\"");
        cmds.Add($"set interface gigabit-ethernet {port} family ethernet-switching native-vlan-id {vlan}");
    }

    /// <summary>Generate BGP neighbor set commands.</summary>
    protected void AddBgpNeighbor(List<string> cmds, string peerIp, string peerAsn, string peerName)
    {
        if (string.IsNullOrEmpty(peerIp) || string.IsNullOrEmpty(peerAsn)) return;
        cmds.Add($"set protocols bgp neighbor {peerIp} remote-as \"{peerAsn}\" bfd");
        cmds.Add($"set protocols bgp neighbor {peerIp} description \"Link-to-{peerName}-AS{peerAsn}\"");
    }

    // ── INotifyPropertyChanged ─────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void N([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Single config line for link master-detail expansion.</summary>
public class LinkConfigLine
{
    public string Side { get; set; } = "";
    public string Command { get; set; } = "";
}
