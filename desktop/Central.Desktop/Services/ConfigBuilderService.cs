using System;
using System.Collections.Generic;
using System.Linq;
using Central.Core.Models;
using Central.Desktop.ViewModels;

namespace Central.Desktop.Services;

/// <summary>
/// Stateless config generation engine. Builds PicOS set commands from toggle state + data collections.
/// </summary>
public class ConfigBuilderService
{
    // ── Section definitions ──────────────────────────────────────────

    public List<BuilderSection> BuildSectionsForDevice(
        DeviceRecord device,
        MainViewModel vm,
        List<(string SectionKey, string ItemKey, bool Enabled)> saved)
    {
        var lookup = saved.ToDictionary(s => (s.SectionKey, s.ItemKey), s => s.Enabled);
        bool S(string sec, string item = "") => lookup.TryGetValue((sec, item), out var v) ? v : true;

        var sections = new List<BuilderSection>();
        var name = device.SwitchName;

        // 1. System
        sections.Add(new BuilderSection { Key = "system", DisplayName = "System", ColorHex = "#569CD6", IsEnabled = S("system") });

        // 2. IP Routing
        sections.Add(new BuilderSection { Key = "ip_routing", DisplayName = "IP Routing", ColorHex = "#569CD6", IsEnabled = S("ip_routing") });

        // 3. QoS / CoS
        sections.Add(new BuilderSection { Key = "cos", DisplayName = "QoS / CoS", ColorHex = "#C586C0", IsEnabled = S("cos") });

        // 4. Voice VLAN
        sections.Add(new BuilderSection { Key = "voice", DisplayName = "Voice VLAN", ColorHex = "#C586C0", IsEnabled = S("voice") });

        // 5. VLANs — each VLAN is toggleable
        var vlanSec = new BuilderSection { Key = "vlans", DisplayName = "VLANs", ColorHex = "#DCDCAA", IsEnabled = S("vlans") };
        foreach (var v in vm.VlanEntries.Where(v => !string.IsNullOrEmpty(v.VlanId) && !v.VlanId.StartsWith("VLAN")))
            vlanSec.Items.Add(new BuilderItem { Key = v.VlanId, DisplayText = $"VLAN {v.VlanId} — {v.Name}", IsEnabled = S("vlans", v.VlanId) });
        sections.Add(vlanSec);

        // 6. L3 Interfaces — from VLANs with gateway
        var l3Sec = new BuilderSection { Key = "l3", DisplayName = "L3 Interfaces", ColorHex = "#6A9955", IsEnabled = S("l3") };
        foreach (var v in vm.VlanEntries.Where(v => !string.IsNullOrEmpty(v.Gateway) && !v.VlanId.StartsWith("VLAN")))
            l3Sec.Items.Add(new BuilderItem { Key = $"vlan-{v.VlanId}", DisplayText = $"vlan-{v.VlanId}  {v.Gateway}/{v.Subnet}", IsEnabled = S("l3", $"vlan-{v.VlanId}") });
        // Loopback
        if (!string.IsNullOrEmpty(device.LoopbackIp))
            l3Sec.Items.Add(new BuilderItem { Key = "lo0", DisplayText = $"lo0  {device.LoopbackIp}", IsEnabled = S("l3", "lo0") });
        sections.Add(l3Sec);

        // 7. P2P Links
        var devP2P = vm.P2PLinks.Where(l =>
            string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.DeviceB, name, StringComparison.OrdinalIgnoreCase)).ToList();
        var p2pSec = new BuilderSection { Key = "p2p", DisplayName = "P2P Links", ColorHex = "#4EC9B0", IsEnabled = S("p2p") };
        foreach (var l in devP2P)
        {
            var peer = string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase) ? l.DeviceB : l.DeviceA;
            p2pSec.Items.Add(new BuilderItem { Key = l.LinkId ?? l.Id.ToString(), DisplayText = $"VL{l.Vlan} {peer} ({l.Subnet})", IsEnabled = S("p2p", l.LinkId ?? l.Id.ToString()) });
        }
        sections.Add(p2pSec);

        // 8. B2B Links
        var devB2B = vm.B2BLinks.Where(l =>
            string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.BuildingB, device.Building, StringComparison.OrdinalIgnoreCase)).ToList();
        var b2bSec = new BuilderSection { Key = "b2b", DisplayName = "B2B Links", ColorHex = "#4EC9B0", IsEnabled = S("b2b") };
        foreach (var l in devB2B)
        {
            var peer = string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase) ? l.BuildingB : l.BuildingA;
            b2bSec.Items.Add(new BuilderItem { Key = l.LinkId ?? l.Id.ToString(), DisplayText = $"B2B VL{l.Vlan} → {peer} ({l.Subnet})", IsEnabled = S("b2b", l.LinkId ?? l.Id.ToString()) });
        }
        sections.Add(b2bSec);

        // 9. BGP
        var bgpSec = new BuilderSection { Key = "bgp", DisplayName = "BGP", ColorHex = "#CE9178", IsEnabled = S("bgp") };
        // Neighbors from enabled P2P links
        foreach (var l in devP2P)
        {
            var isA = string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase);
            var peerIp = isA ? l.DeviceBIp : l.DeviceAIp;
            if (!string.IsNullOrEmpty(peerIp))
                bgpSec.Items.Add(new BuilderItem { Key = $"p2p-{peerIp}", DisplayText = $"P2P peer {peerIp}", IsEnabled = S("bgp", $"p2p-{peerIp}") });
        }
        foreach (var l in devB2B)
        {
            var isA = string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase);
            var peerIp = isA ? l.DeviceBIp : l.DeviceAIp;
            if (!string.IsNullOrEmpty(peerIp))
                bgpSec.Items.Add(new BuilderItem { Key = $"b2b-{peerIp}", DisplayText = $"B2B peer {peerIp}", IsEnabled = S("bgp", $"b2b-{peerIp}") });
        }
        sections.Add(bgpSec);

        // 10. MSTP
        sections.Add(new BuilderSection { Key = "mstp", DisplayName = "MSTP", ColorHex = "#DCDCAA", IsEnabled = S("mstp") });

        // 11. MLAG
        sections.Add(new BuilderSection { Key = "mlag", DisplayName = "MLAG", ColorHex = "#4EC9B0", IsEnabled = S("mlag") });

        // 12. VRRP — from selected VLANs
        var vrrpSec = new BuilderSection { Key = "vrrp", DisplayName = "VRRP", ColorHex = "#6A9955", IsEnabled = S("vrrp") };
        foreach (var v in vm.VlanEntries.Where(v => !string.IsNullOrEmpty(v.Gateway) && !v.VlanId.StartsWith("VLAN")))
            vrrpSec.Items.Add(new BuilderItem { Key = $"vlan-{v.VlanId}", DisplayText = $"VRRP vlan-{v.VlanId} VIP {v.Gateway}", IsEnabled = S("vrrp", $"vlan-{v.VlanId}") });
        sections.Add(vrrpSec);

        // 13. DHCP Relay
        var dhcpSec = new BuilderSection { Key = "dhcp", DisplayName = "DHCP Relay", ColorHex = "#D7BA7D", IsEnabled = S("dhcp") };
        sections.Add(dhcpSec);

        // 14. Static Routes
        sections.Add(new BuilderSection { Key = "static_routes", DisplayName = "Static Routes", ColorHex = "#CE9178", IsEnabled = S("static_routes") });

        // 15. LLDP
        sections.Add(new BuilderSection { Key = "lldp", DisplayName = "LLDP", ColorHex = "#D7BA7D", IsEnabled = S("lldp") });

        // 16. Management
        sections.Add(new BuilderSection { Key = "management", DisplayName = "Management", ColorHex = "#569CD6", IsEnabled = S("management") });

        return sections;
    }

    // ── Config generation ────────────────────────────────────────────

    public List<ConfigLine> Generate(
        DeviceRecord device,
        List<BuilderSection> sections,
        MainViewModel vm)
    {
        var lines = new List<ConfigLine>();
        var sectionMap = sections.ToDictionary(s => s.Key);
        var name = device.SwitchName;

        void Add(string sectionKey, string text) => lines.Add(new ConfigLine(text, sectionKey));
        void Set(string sectionKey, params string[] parts) => Add(sectionKey, "set " + string.Join(" ", parts));

        bool Enabled(string key) => sectionMap.TryGetValue(key, out var s) && s.IsEnabled;
        bool ItemEnabled(string sectionKey, string itemKey)
        {
            if (!sectionMap.TryGetValue(sectionKey, out var s) || !s.IsEnabled) return false;
            var item = s.Items.FirstOrDefault(i => i.Key == itemKey);
            return item == null || item.IsEnabled; // default enabled if no matching item
        }

        // ── 1. System ──
        if (Enabled("system"))
        {
            Set("system", "system hostname", $"\"{name}\"");
            if (!string.IsNullOrEmpty(device.Asn))
                Add("system", $"# ASN: {device.Asn}");
        }

        // ── 2. IP Routing ──
        if (Enabled("ip_routing"))
            Set("ip_routing", "ip routing enable true");

        // ── 3. QoS / CoS ──
        if (Enabled("cos"))
        {
            foreach (var line in GetDefaultQoSLines(device))
                Add("cos", line);
        }

        // ── 4. Voice VLAN ──
        if (Enabled("voice"))
        {
            foreach (var line in DefaultVoiceVlan)
                Add("voice", line);
        }

        // ── 5. VLANs ──
        if (Enabled("vlans"))
        {
            foreach (var v in vm.VlanEntries.Where(v => !v.VlanId.StartsWith("VLAN")))
            {
                if (!ItemEnabled("vlans", v.VlanId)) continue;
                if (!string.IsNullOrEmpty(v.Name))
                    Set("vlans", "vlans vlan-id", v.VlanId, "description", $"\"{v.Name}\"");
                else
                    Set("vlans", "vlans vlan-id", v.VlanId);
            }
        }

        // ── 6. L3 Interfaces ──
        if (Enabled("l3"))
        {
            // Loopback
            if (!string.IsNullOrEmpty(device.LoopbackIp) && ItemEnabled("l3", "lo0"))
            {
                var prefix = device.LoopbackSubnet?.Contains("/") == true
                    ? device.LoopbackSubnet.Split('/').Last() : "32";
                Set("l3", "l3-interface loopback lo0 address", device.LoopbackIp, "prefix-length", prefix);
            }
            // SVIs from VLANs
            foreach (var v in vm.VlanEntries.Where(v => !string.IsNullOrEmpty(v.Gateway) && !v.VlanId.StartsWith("VLAN")))
            {
                if (!ItemEnabled("l3", $"vlan-{v.VlanId}")) continue;
                if (!ItemEnabled("vlans", v.VlanId)) continue; // skip if parent VLAN disabled
                var prefix = ExtractPrefix(v.Subnet);
                Set("l3", "l3-interface vlan-interface", $"vlan-{v.VlanId}", "address", v.Gateway, "prefix-length", prefix);
            }
        }

        // ── 7. P2P Links ──
        if (Enabled("p2p"))
        {
            var devP2P = vm.P2PLinks.Where(l =>
                string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.DeviceB, name, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var l in devP2P)
            {
                var key = l.LinkId ?? l.Id.ToString();
                if (!ItemEnabled("p2p", key)) continue;
                var isA = string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase);
                var myIp = isA ? l.DeviceAIp : l.DeviceBIp;
                var myPort = isA ? l.PortA : l.PortB;
                var peer = isA ? l.DeviceB : l.DeviceA;
                var prefix = ExtractPrefix(l.Subnet);

                Set("p2p", "vlans vlan-id", l.Vlan, "description", $"\"P2P-{peer}\"");
                Set("p2p", "vlans vlan-id", l.Vlan, "l3-interface", $"\"vlan-{l.Vlan}\"");
                Set("p2p", "l3-interface vlan-interface", $"vlan-{l.Vlan}", "address", myIp, "prefix-length", prefix);
                if (!string.IsNullOrEmpty(myPort))
                {
                    Set("p2p", "interface gigabit-ethernet", myPort, "description", $"\"P2P-{peer}\"");
                    Set("p2p", "interface gigabit-ethernet", myPort, "family ethernet-switching native-vlan-id", l.Vlan);
                    Set("p2p", "interface gigabit-ethernet", myPort, "family ethernet-switching port-mode", "\"trunk\"");
                }
            }
        }

        // ── 8. B2B Links ──
        if (Enabled("b2b"))
        {
            var devB2B = vm.B2BLinks.Where(l =>
                string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.BuildingB, device.Building, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var l in devB2B)
            {
                var key = l.LinkId ?? l.Id.ToString();
                if (!ItemEnabled("b2b", key)) continue;
                var isA = string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase);
                var myIp = isA ? l.DeviceAIp : l.DeviceBIp;
                var peer = isA ? l.BuildingB : l.BuildingA;
                var prefix = ExtractPrefix(l.Subnet);

                Set("b2b", "vlans vlan-id", l.Vlan, "description", $"\"B2B-{peer}\"");
                Set("b2b", "vlans vlan-id", l.Vlan, "l3-interface", $"\"vlan-{l.Vlan}\"");
                Set("b2b", "l3-interface vlan-interface", $"vlan-{l.Vlan}", "address", myIp, "prefix-length", prefix);
            }
        }

        // ── 9. BGP ──
        if (Enabled("bgp") && !string.IsNullOrEmpty(device.Asn))
        {
            Set("bgp", "protocols bgp local-as", $"\"{device.Asn}\"");
            Set("bgp", "protocols bgp ebgp-requires-policy false");
            if (!string.IsNullOrEmpty(device.LoopbackIp))
                Set("bgp", "protocols bgp router-id", device.LoopbackIp);

            // Neighbors from P2P
            var devP2P = vm.P2PLinks.Where(l =>
                string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.DeviceB, name, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var l in devP2P)
            {
                var isA = string.Equals(l.DeviceA, name, StringComparison.OrdinalIgnoreCase);
                var peerIp = isA ? l.DeviceBIp : l.DeviceAIp;
                var peerName = isA ? l.DeviceB : l.DeviceA;
                if (string.IsNullOrEmpty(peerIp)) continue;
                if (!ItemEnabled("bgp", $"p2p-{peerIp}")) continue;
                // Find peer ASN
                var peerDev = vm.Devices.FirstOrDefault(d => string.Equals(d.SwitchName, peerName, StringComparison.OrdinalIgnoreCase));
                var peerAs = peerDev?.Asn ?? "?";
                Set("bgp", "protocols bgp neighbor", peerIp, "remote-as", $"\"{peerAs}\"");
                Set("bgp", "protocols bgp neighbor", peerIp, "description", $"\"P2P-{peerName}\"");
            }

            // Neighbors from B2B
            var devB2B = vm.B2BLinks.Where(l =>
                string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(l.BuildingB, device.Building, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var l in devB2B)
            {
                var isA = string.Equals(l.BuildingA, device.Building, StringComparison.OrdinalIgnoreCase);
                var peerIp = isA ? l.DeviceBIp : l.DeviceAIp;
                if (string.IsNullOrEmpty(peerIp)) continue;
                if (!ItemEnabled("bgp", $"b2b-{peerIp}")) continue;
                var peerBldg = isA ? l.BuildingB : l.BuildingA;
                Set("bgp", "protocols bgp neighbor", peerIp, "remote-as", "\"?\"");
                Set("bgp", "protocols bgp neighbor", peerIp, "description", $"\"B2B-{peerBldg}\"");
            }

            Set("bgp", "protocols bgp ipv4-unicast redistribute connected");
            Set("bgp", "protocols bgp ipv4-unicast multipath ebgp maximum-paths 4");
        }

        // ── 10. MSTP ──
        if (Enabled("mstp"))
        {
            var mstp = vm.MstpConfigs.FirstOrDefault(m =>
                string.Equals(m.DeviceName, name, StringComparison.OrdinalIgnoreCase));
            if (mstp != null && !string.IsNullOrEmpty(mstp.MstpPriority))
                Set("mstp", "protocols spanning-tree mstp bridge-priority", mstp.MstpPriority);
        }

        // ── 11. MLAG ──
        if (Enabled("mlag"))
        {
            var mlag = vm.MlagConfigs.FirstOrDefault(m =>
                string.Equals(m.SwitchA, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.SwitchB, name, StringComparison.OrdinalIgnoreCase));
            if (mlag != null)
            {
                Set("mlag", "protocols mlag domain", mlag.MlagDomain);
                if (!string.IsNullOrEmpty(mlag.PeerLinkAe))
                    Set("mlag", "protocols mlag peer-link", mlag.PeerLinkAe);
            }
        }

        // ── 12. VRRP ──
        if (Enabled("vrrp"))
        {
            foreach (var v in vm.VlanEntries.Where(v => !string.IsNullOrEmpty(v.Gateway) && !v.VlanId.StartsWith("VLAN")))
            {
                if (!ItemEnabled("vrrp", $"vlan-{v.VlanId}")) continue;
                if (!ItemEnabled("vlans", v.VlanId)) continue;
                Set("vrrp", "protocols vrrp interface", $"vlan-{v.VlanId}", "vrid 1 ip", v.Gateway);
            }
        }

        // ── 13. DHCP Relay ──
        if (Enabled("dhcp"))
        {
            Set("dhcp", "protocols dhcp relay interface vlan-120 dhcp-server-address 10.11.120.10");
            Set("dhcp", "protocols dhcp relay interface vlan-120 dhcp-server-address 10.11.120.11");
        }

        // ── 14. Static Routes ──
        if (Enabled("static_routes"))
        {
            Set("static_routes", "protocols static route 0.0.0.0/0 next-hop 10.11.152.254");
        }

        // ── 15. LLDP ──
        if (Enabled("lldp"))
            Set("lldp", "protocols lldp enable true");

        // ── 16. Management ──
        if (Enabled("management") && !string.IsNullOrEmpty(device.ManagementIp))
        {
            Set("management", $"# Management IP: {device.ManagementIp}");
        }

        // Update line counts on sections
        foreach (var sec in sections)
            sec.LineCount = lines.Count(l => l.SectionKey == sec.Key);

        return lines;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string ExtractPrefix(string? subnet)
    {
        if (string.IsNullOrEmpty(subnet)) return "24";
        var slash = subnet.LastIndexOf('/');
        return slash >= 0 ? subnet[(slash + 1)..] : "24";
    }

    // ── Default QoS template ────────────────────────────────────────

    private static List<string> GetDefaultQoSLines(DeviceRecord device)
    {
        var lines = new List<string>
        {
            "set class-of-service forwarding-class fc-best-effort",
            "set class-of-service forwarding-class fc-bulk local-priority 1",
            "set class-of-service forwarding-class fc-network-control local-priority 7",
            "set class-of-service forwarding-class fc-realtime local-priority 4",
            "set class-of-service forwarding-class fc-signaling local-priority 3",
            "set class-of-service forwarding-class fc-transactional local-priority 2",
            "set class-of-service forwarding-class fc-video local-priority 5",
            "set class-of-service forwarding-class fc-voice-ef local-priority 6",
            "set class-of-service scheduler sched-network-control mode \"SP\"",
            "set class-of-service scheduler sched-voice mode \"SP\"",
            "set class-of-service scheduler sched-video mode \"WFQ\"",
            "set class-of-service scheduler sched-video weight 6",
            "set class-of-service scheduler sched-realtime mode \"WFQ\"",
            "set class-of-service scheduler sched-realtime weight 4",
            "set class-of-service scheduler sched-signaling mode \"WFQ\"",
            "set class-of-service scheduler sched-signaling weight 2",
            "set class-of-service scheduler sched-transactional mode \"WFQ\"",
            "set class-of-service scheduler sched-transactional weight 6",
            "set class-of-service scheduler sched-bulk mode \"WFQ\"",
            "set class-of-service scheduler sched-bulk weight 2",
            "set class-of-service scheduler sched-best-effort mode \"WFQ\"",
            "set class-of-service scheduler sched-best-effort weight 10",
            "set class-of-service classifier qos-dscp-classifier trust-mode \"dscp\"",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-network-control code-point 48",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-network-control code-point 56",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-voice-ef code-point 44",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-voice-ef code-point 46",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 34",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 36",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 38",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 40",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 26",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 28",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 30",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 32",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 18",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 20",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 22",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 24",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 10",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 12",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 14",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 16",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-bulk code-point 1",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-bulk code-point 8",
            "set class-of-service classifier qos-dscp-classifier forwarding-class fc-best-effort code-point 0",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-network-control scheduler \"sched-network-control\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-voice-ef scheduler \"sched-voice\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-video scheduler \"sched-video\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-realtime scheduler \"sched-realtime\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-signaling scheduler \"sched-signaling\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-transactional scheduler \"sched-transactional\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-bulk scheduler \"sched-bulk\"",
            "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-best-effort scheduler \"sched-best-effort\"",
        };

        // Per-interface QoS bindings — generate for all ports based on device type
        var dt = (device.DeviceType ?? "").ToLower();
        var interfaces = new List<string>();

        if (dt.Contains("l2") || dt.Contains("leaf") || dt.Contains("management"))
        {
            // GE ports (1G copper)
            for (int i = 1; i <= 48; i++) interfaces.Add($"ge-1/1/{i}");
            for (int i = 1; i <= 4; i++) interfaces.Add($"te-1/1/{i}");
        }
        else
        {
            // XE ports (10G/25G/100G SFP+)
            for (int i = 1; i <= 32; i++) interfaces.Add($"xe-1/1/{i}");
        }

        foreach (var iface in interfaces)
        {
            lines.Add($"set class-of-service interface {iface} classifier \"qos-dscp-classifier\"");
            lines.Add($"set class-of-service interface {iface} scheduler-profile \"qos-flex-profile\"");
        }

        return lines;
    }

    private static readonly string[] DefaultVoiceVlan =
    {
        "set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 mask ff:ff:ff:00:00:00",
        "set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 description \"Avaya\"",
        "set vlans voice-vlan local-priority 6",
        "set vlans voice-vlan dscp 46",
    };
}
