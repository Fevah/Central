"""
PicOS 4.x Configuration Parser
================================
Parses FS/PicOS switch configuration files (set-style CLI format)
and produces structured Python dicts ready for PostgreSQL insertion.

Config sections handled:
  - system (hostname, management-vrf, inband, ssh)
  - vlans
  - interfaces (gigabit-ethernet, aggregate-ethernet, breakout)
  - l3-interface (SVI + loopback)
  - protocols.bgp (neighbors, networks, multipath)
  - protocols.vrrp
  - protocols.dhcp relay
  - protocols.static route
  - protocols.spanning-tree
  - protocols.lldp
  - class-of-service forwarding-class
  - firewall filter
  - ip routing

Usage:
    parser = PicOSParser("MEP-91-CORE02.txt")
    data   = parser.parse()
    # data is a SwitchConfig dataclass
"""

from __future__ import annotations

import re
import os
from dataclasses import dataclass, field
from typing import Optional


# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class ForwardingClass:
    class_name: str
    local_priority: Optional[int] = None


@dataclass
class FirewallFilterEntry:
    filter_name: str
    sequence: int
    from_params: dict = field(default_factory=dict)
    then_params: dict = field(default_factory=dict)
    input_interface: Optional[str] = None


@dataclass
class Interface:
    interface_name: str
    description: Optional[str] = None
    speed: Optional[str] = None
    mtu: Optional[int] = None
    fec: bool = False
    breakout: bool = False
    native_vlan_id: Optional[int] = None
    port_mode: Optional[str] = None
    vlan_members: Optional[str] = None
    aggregate_member: Optional[str] = None
    voice_vlans: list = field(default_factory=list)  # [{vlan_id, mode, tagged_mode}]


@dataclass
class L3Interface:
    interface_name: str   # e.g. vlan-100, lo0
    ip_address: Optional[str] = None
    prefix_length: Optional[int] = None
    description: Optional[str] = None


@dataclass
class BgpNeighbor:
    neighbor_ip: str
    remote_as: Optional[int] = None
    description: Optional[str] = None
    bfd_enabled: bool = False
    ipv4_unicast: bool = False


@dataclass
class BgpConfig:
    local_as: Optional[int] = None
    router_id: Optional[str] = None
    ebgp_requires_policy: bool = False
    max_paths: int = 4
    neighbors: list = field(default_factory=list)   # List[BgpNeighbor]
    networks: list = field(default_factory=list)    # List[str] (CIDR)
    redistribute_connected: bool = False


@dataclass
class VrrpEntry:
    interface_name: str
    vrid: int
    virtual_ip: Optional[str] = None
    load_balance: bool = False


@dataclass
class DhcpRelay:
    interface_name: str
    dhcp_server_addresses: list = field(default_factory=list)
    relay_agent_address: Optional[str] = None
    disabled: bool = False


@dataclass
class StaticRoute:
    prefix: str
    next_hop: str


@dataclass
class SpanningTree:
    protocol: str = "mstp"
    bridge_priority: Optional[int] = None


@dataclass
class Vlan:
    vlan_id: int
    description: Optional[str] = None
    l3_interface: Optional[str] = None


@dataclass
class SwitchConfig:
    """Top-level parsed switch configuration."""
    source_file: str
    hostname: Optional[str] = None
    site: Optional[str] = None
    role: Optional[str] = None
    management_vrf: bool = False
    inband_enabled: bool = False
    ssh_root_login: str = "allow"
    ip_routing: bool = False
    loopback_ip: Optional[str] = None
    loopback_prefix: Optional[int] = None

    vlans: dict = field(default_factory=dict)            # {vlan_id: Vlan}
    interfaces: dict = field(default_factory=dict)       # {iface_name: Interface}
    l3_interfaces: dict = field(default_factory=dict)    # {iface_name: L3Interface}
    bgp: Optional[BgpConfig] = None
    vrrp: list = field(default_factory=list)             # List[VrrpEntry]
    dhcp_relay: list = field(default_factory=list)       # List[DhcpRelay]
    static_routes: list = field(default_factory=list)    # List[StaticRoute]
    spanning_tree: Optional[SpanningTree] = None
    cos_forwarding_classes: list = field(default_factory=list)  # List[ForwardingClass]
    firewall_filters: list = field(default_factory=list)        # List[FirewallFilterEntry]


# ---------------------------------------------------------------------------
# Parser
# ---------------------------------------------------------------------------

class PicOSParser:
    """
    Parses a PicOS set-style config file line by line.

    Each line has the form:
        set <path...> <value>

    Leading whitespace is stripped.  Lines starting with '#' are comments.
    """

    # Regex for interface name like xe-1/1/1, xe-1/1/31.2, ge-1/1/5, ae36
    IFACE_RE = re.compile(
        r'^(?:gigabit-ethernet\s+)?(xe-\d+/\d+/\d+(?:\.\d+)?|ge-\d+/\d+/\d+(?:\.\d+)?|ae\d+)\s*'
    )

    def __init__(self, filepath: str):
        self.filepath = filepath
        self.filename = os.path.basename(filepath)
        self._cfg: SwitchConfig = SwitchConfig(source_file=self.filename)

    # -----------------------------------------------------------------------
    # Public
    # -----------------------------------------------------------------------

    def parse(self) -> SwitchConfig:
        with open(self.filepath, "r", encoding="utf-8") as fh:
            for raw_line in fh:
                line = raw_line.strip()
                if not line or line.startswith("#"):
                    continue
                if not line.startswith("set "):
                    continue
                tokens = self._tokenize(line[4:])  # strip leading "set "
                self._dispatch(tokens)

        # Derive site / role from hostname after parse
        self._derive_site_role()
        return self._cfg

    # -----------------------------------------------------------------------
    # Tokenizer
    # -----------------------------------------------------------------------

    def _tokenize(self, text: str) -> list[str]:
        """
        Split on whitespace but keep quoted strings together.
        e.g.  description "My Switch"  ->  ['description', 'My Switch']
        """
        tokens = []
        current = []
        in_quote = False
        for ch in text:
            if ch == '"':
                in_quote = not in_quote
            elif ch == ' ' and not in_quote:
                if current:
                    tokens.append("".join(current))
                    current = []
            else:
                current.append(ch)
        if current:
            tokens.append("".join(current))
        return tokens

    # -----------------------------------------------------------------------
    # Dispatcher
    # -----------------------------------------------------------------------

    def _dispatch(self, tokens: list[str]):
        if not tokens:
            return
        section = tokens[0]
        rest = tokens[1:]

        dispatch_map = {
            "system":               self._parse_system,
            "vlans":                self._parse_vlan,
            "interface":            self._parse_interface,
            "l3-interface":         self._parse_l3_interface,
            "protocols":            self._parse_protocols,
            "ip":                   self._parse_ip,
            "class-of-service":     self._parse_cos,
            "firewall":             self._parse_firewall,
        }
        handler = dispatch_map.get(section)
        if handler:
            handler(rest)

    # -----------------------------------------------------------------------
    # system
    # -----------------------------------------------------------------------

    def _parse_system(self, tokens: list[str]):
        if not tokens:
            return
        key = tokens[0]
        if key == "hostname" and len(tokens) >= 2:
            self._cfg.hostname = tokens[1]
        elif key == "management-vrf" and len(tokens) >= 3 and tokens[1] == "enable":
            self._cfg.management_vrf = tokens[2].lower() == "true"
        elif key == "inband" and len(tokens) >= 3 and tokens[1] == "enable":
            self._cfg.inband_enabled = tokens[2].lower() == "true"
        elif key == "services" and len(tokens) >= 4:
            # services ssh root-login allow
            if tokens[1] == "ssh" and tokens[2] == "root-login":
                self._cfg.ssh_root_login = tokens[3]

    # -----------------------------------------------------------------------
    # vlans
    # -----------------------------------------------------------------------

    def _parse_vlan(self, tokens: list[str]):
        # vlans vlan-id <id> description|l3-interface <value>
        if len(tokens) < 3 or tokens[0] != "vlan-id":
            return
        try:
            vlan_id = int(tokens[1])
        except ValueError:
            return

        vlan = self._cfg.vlans.setdefault(vlan_id, Vlan(vlan_id=vlan_id))
        attr = tokens[2] if len(tokens) > 2 else None

        if attr == "description" and len(tokens) >= 4:
            vlan.description = tokens[3]
        elif attr == "l3-interface" and len(tokens) >= 4:
            vlan.l3_interface = tokens[3]

    # -----------------------------------------------------------------------
    # interface
    # -----------------------------------------------------------------------

    def _parse_interface(self, tokens: list[str]):
        if not tokens:
            return

        # interface ecmp max-path 4
        if tokens[0] == "ecmp":
            return

        # interface aggregate-ethernet ae36
        if tokens[0] == "aggregate-ethernet":
            if len(tokens) >= 2:
                iface_name = tokens[1]
                self._cfg.interfaces.setdefault(iface_name, Interface(interface_name=iface_name))
            return

        # interface gigabit-ethernet <name> [attrib...]
        if tokens[0] != "gigabit-ethernet":
            return
        if len(tokens) < 2:
            return

        iface_name = tokens[1]
        iface = self._cfg.interfaces.setdefault(iface_name, Interface(interface_name=iface_name))
        rest = tokens[2:]

        if not rest:
            return

        key = rest[0]

        if key == "description" and len(rest) >= 2:
            iface.description = rest[1]

        elif key == "speed" and len(rest) >= 2:
            iface.speed = rest[1]

        elif key == "mtu" and len(rest) >= 2:
            try:
                iface.mtu = int(rest[1])
            except ValueError:
                pass

        elif key == "fec" and len(rest) >= 2:
            iface.fec = rest[1].lower() == "true"

        elif key == "breakout" and len(rest) >= 2:
            iface.breakout = rest[1].lower() == "true"

        elif key == "family" and len(rest) >= 2 and rest[1] == "ethernet-switching":
            sub = rest[2] if len(rest) > 2 else None
            if sub == "native-vlan-id" and len(rest) >= 4:
                try:
                    iface.native_vlan_id = int(rest[3])
                except ValueError:
                    pass
            elif sub == "port-mode" and len(rest) >= 4:
                iface.port_mode = rest[3]
            elif sub == "vlan" and len(rest) >= 5 and rest[3] == "members":
                iface.vlan_members = rest[4]

        elif key == "voice-vlan":
            sub = rest[1] if len(rest) > 1 else None
            # voice-vlan vlan-id 136
            if sub == "vlan-id" and len(rest) >= 3:
                voice_entry = self._get_or_create_voice_vlan(iface)
                try:
                    voice_entry["vlan_id"] = int(rest[2])
                except ValueError:
                    pass
            # voice-vlan mode manual
            elif sub == "mode" and len(rest) >= 3:
                voice_entry = self._get_or_create_voice_vlan(iface)
                voice_entry["mode"] = rest[2]
            # voice-vlan tagged mode tag
            elif sub == "tagged" and len(rest) >= 4 and rest[2] == "mode":
                voice_entry = self._get_or_create_voice_vlan(iface)
                voice_entry["tagged_mode"] = rest[3]

    def _get_or_create_voice_vlan(self, iface: Interface) -> dict:
        if not iface.voice_vlans:
            iface.voice_vlans.append({})
        return iface.voice_vlans[-1]

    # -----------------------------------------------------------------------
    # l3-interface
    # -----------------------------------------------------------------------

    def _parse_l3_interface(self, tokens: list[str]):
        # l3-interface loopback lo0 address <ip> prefix-length <int>
        # l3-interface vlan-interface vlan-<id> address <ip> prefix-length <int>
        if len(tokens) < 2:
            return

        iface_type = tokens[0]   # loopback | vlan-interface
        iface_name = tokens[1]   # lo0 | vlan-100

        l3 = self._cfg.l3_interfaces.setdefault(
            iface_name, L3Interface(interface_name=iface_name)
        )

        rest = tokens[2:]
        if not rest:
            return

        key = rest[0]
        if key == "address" and len(rest) >= 2:
            l3.ip_address = rest[1]
            # If loopback, capture at top level too
            if iface_type == "loopback":
                self._cfg.loopback_ip = rest[1]

        elif key == "prefix-length" and len(rest) >= 2:
            try:
                pl = int(rest[1])
                l3.prefix_length = pl
                if iface_type == "loopback":
                    self._cfg.loopback_prefix = pl
            except ValueError:
                pass

        elif key == "description" and len(rest) >= 2:
            l3.description = rest[1]

    # -----------------------------------------------------------------------
    # protocols
    # -----------------------------------------------------------------------

    def _parse_protocols(self, tokens: list[str]):
        if not tokens:
            return
        proto = tokens[0]
        rest = tokens[1:]

        proto_map = {
            "bgp":           self._parse_bgp,
            "vrrp":          self._parse_vrrp,
            "dhcp":          self._parse_dhcp,
            "static":        self._parse_static,
            "spanning-tree": self._parse_stp,
            "lldp":          self._parse_lldp,
            "bfd":           lambda _: None,   # just enable marker
        }
        handler = proto_map.get(proto)
        if handler:
            handler(rest)

    # BGP
    def _parse_bgp(self, tokens: list[str]):
        if self._cfg.bgp is None:
            self._cfg.bgp = BgpConfig()
        bgp = self._cfg.bgp

        if not tokens:
            return
        key = tokens[0]

        if key == "local-as" and len(tokens) >= 2:
            try:
                bgp.local_as = int(tokens[1])
            except ValueError:
                pass

        elif key == "router-id" and len(tokens) >= 2:
            bgp.router_id = tokens[1]

        elif key == "ebgp-requires-policy" and len(tokens) >= 2:
            bgp.ebgp_requires_policy = tokens[1].lower() == "true"

        elif key == "neighbor" and len(tokens) >= 3:
            neighbor_ip = tokens[1]
            neighbor = self._get_or_create_bgp_neighbor(bgp, neighbor_ip)
            attr = tokens[2]
            if attr == "remote-as" and len(tokens) >= 4:
                try:
                    neighbor.remote_as = int(tokens[3])
                except ValueError:
                    pass
            elif attr == "description" and len(tokens) >= 4:
                neighbor.description = tokens[3]
            elif attr == "bfd":
                neighbor.bfd_enabled = True
            elif attr == "ipv4-unicast":
                neighbor.ipv4_unicast = True

        elif key == "ipv4-unicast":
            sub = tokens[1] if len(tokens) > 1 else None
            if sub == "network" and len(tokens) >= 3:
                if tokens[2] not in bgp.networks:
                    bgp.networks.append(tokens[2])
            elif sub == "redistribute" and len(tokens) >= 3 and tokens[2] == "connected":
                bgp.redistribute_connected = True
            elif sub == "multipath" and len(tokens) >= 5:
                # multipath ebgp maximum-paths 4
                try:
                    bgp.max_paths = int(tokens[4])
                except (ValueError, IndexError):
                    pass

    def _get_or_create_bgp_neighbor(self, bgp: BgpConfig, ip: str) -> BgpNeighbor:
        for n in bgp.neighbors:
            if n.neighbor_ip == ip:
                return n
        n = BgpNeighbor(neighbor_ip=ip)
        bgp.neighbors.append(n)
        return n

    # VRRP
    def _parse_vrrp(self, tokens: list[str]):
        # vrrp interface vlan-1 vrid 1 ip 10.11.1.254
        # vrrp interface vlan-1 vrid 1 load-balance disable false
        if len(tokens) < 2 or tokens[0] != "interface":
            return
        iface_name = tokens[1]
        rest = tokens[2:]

        if not rest or rest[0] != "vrid":
            return
        try:
            vrid = int(rest[1])
        except (ValueError, IndexError):
            return

        vrrp = self._get_or_create_vrrp(iface_name, vrid)
        sub = rest[2] if len(rest) > 2 else None
        if sub == "ip" and len(rest) >= 4:
            vrrp.virtual_ip = rest[3]
        elif sub == "load-balance" and len(rest) >= 5:
            # load-balance disable false  means load-balance IS enabled
            vrrp.load_balance = (rest[3] == "disable" and rest[4].lower() == "false")

    def _get_or_create_vrrp(self, iface: str, vrid: int) -> VrrpEntry:
        for v in self._cfg.vrrp:
            if v.interface_name == iface and v.vrid == vrid:
                return v
        v = VrrpEntry(interface_name=iface, vrid=vrid)
        self._cfg.vrrp.append(v)
        return v

    # DHCP relay
    def _parse_dhcp(self, tokens: list[str]):
        # dhcp relay interface vlan-112 disable false
        # dhcp relay interface vlan-112 dhcp-server-address 10.11.120.10
        # dhcp relay interface vlan-112 relay-agent-address 10.11.119.254
        if len(tokens) < 2 or tokens[0] != "relay":
            return
        rest = tokens[1:]
        if not rest or rest[0] != "interface":
            return
        iface_name = rest[1]
        relay = self._get_or_create_dhcp_relay(iface_name)

        sub = rest[2] if len(rest) > 2 else None
        if sub == "disable" and len(rest) >= 4:
            relay.disabled = rest[3].lower() == "true"
        elif sub == "dhcp-server-address" and len(rest) >= 4:
            addr = rest[3]
            if addr not in relay.dhcp_server_addresses:
                relay.dhcp_server_addresses.append(addr)
        elif sub == "relay-agent-address" and len(rest) >= 4:
            relay.relay_agent_address = rest[3]

    def _get_or_create_dhcp_relay(self, iface: str) -> DhcpRelay:
        for d in self._cfg.dhcp_relay:
            if d.interface_name == iface:
                return d
        d = DhcpRelay(interface_name=iface)
        self._cfg.dhcp_relay.append(d)
        return d

    # Static routes
    def _parse_static(self, tokens: list[str]):
        # static route 0.0.0.0/0 next-hop 10.11.159.200
        if len(tokens) < 4 or tokens[0] != "route":
            return
        prefix = tokens[1]
        if tokens[2] == "next-hop":
            next_hop = tokens[3]
            self._cfg.static_routes.append(StaticRoute(prefix=prefix, next_hop=next_hop))

    # STP
    def _parse_stp(self, tokens: list[str]):
        if not tokens:
            return
        if self._cfg.spanning_tree is None:
            self._cfg.spanning_tree = SpanningTree()
        stp = self._cfg.spanning_tree

        if tokens[0] == "mstp":
            stp.protocol = "mstp"
            if len(tokens) >= 3 and tokens[1] == "bridge-priority":
                try:
                    stp.bridge_priority = int(tokens[2])
                except ValueError:
                    pass
        elif tokens[0] == "rstp":
            stp.protocol = "rstp"

    # LLDP
    def _parse_lldp(self, tokens: list[str]):
        # lldp enable true  — no action needed beyond noting it
        pass

    # -----------------------------------------------------------------------
    # ip routing
    # -----------------------------------------------------------------------

    def _parse_ip(self, tokens: list[str]):
        if len(tokens) >= 2 and tokens[0] == "routing" and tokens[1] == "enable":
            self._cfg.ip_routing = (len(tokens) < 3 or tokens[2].lower() == "true")

    # -----------------------------------------------------------------------
    # class-of-service
    # -----------------------------------------------------------------------

    def _parse_cos(self, tokens: list[str]):
        # class-of-service forwarding-class <name> local-priority <int>
        if not tokens or tokens[0] != "forwarding-class":
            return
        if len(tokens) < 2:
            return
        class_name = tokens[1]
        fc = self._get_or_create_fc(class_name)

        if len(tokens) >= 4 and tokens[2] == "local-priority":
            try:
                fc.local_priority = int(tokens[3])
            except ValueError:
                pass

    def _get_or_create_fc(self, name: str) -> ForwardingClass:
        for fc in self._cfg.cos_forwarding_classes:
            if fc.class_name == name:
                return fc
        fc = ForwardingClass(class_name=name)
        self._cfg.cos_forwarding_classes.append(fc)
        return fc

    # -----------------------------------------------------------------------
    # firewall filter
    # -----------------------------------------------------------------------

    def _parse_firewall(self, tokens: list[str]):
        # firewall filter <name> sequence <seq> then|from <key> <val>
        # firewall filter <name> input vlan-interface <iface>
        if not tokens or tokens[0] != "filter":
            return
        if len(tokens) < 3:
            return

        filter_name = tokens[1]
        rest = tokens[2:]

        if rest[0] == "input" and len(rest) >= 3 and rest[1] == "vlan-interface":
            # Record the input interface on all entries for this filter
            for entry in self._cfg.firewall_filters:
                if entry.filter_name == filter_name:
                    entry.input_interface = rest[2]
            return

        if rest[0] != "sequence":
            return
        try:
            seq = int(rest[1])
        except (ValueError, IndexError):
            return

        entry = self._get_or_create_fw_entry(filter_name, seq)
        sub_tokens = rest[2:]
        if not sub_tokens:
            return

        direction = sub_tokens[0]   # "then" or "from"
        sub_rest = sub_tokens[1:]

        if direction == "then" and len(sub_rest) >= 1:
            key = sub_rest[0]
            val = sub_rest[1] if len(sub_rest) > 1 else True
            entry.then_params[key] = val

        elif direction == "from" and len(sub_rest) >= 1:
            key = sub_rest[0]
            val = sub_rest[1] if len(sub_rest) > 1 else True
            entry.from_params[key] = val

    def _get_or_create_fw_entry(self, filter_name: str, seq: int) -> FirewallFilterEntry:
        for e in self._cfg.firewall_filters:
            if e.filter_name == filter_name and e.sequence == seq:
                return e
        e = FirewallFilterEntry(filter_name=filter_name, sequence=seq)
        self._cfg.firewall_filters.append(e)
        return e

    # -----------------------------------------------------------------------
    # Derive site / role
    # -----------------------------------------------------------------------

    def _derive_site_role(self):
        """
        Infer site and role from hostname.
        Naming convention: MEP-{site_num}-{ROLE}{optional_num}
        Examples:
            MEP-91-CORE02  ->  site=MEP-91, role=core
            MEP-92-CORE01  ->  site=MEP-92, role=core
            MEP-93-L1-CORE02 -> site=MEP-93, role=l1
            MEP-96-L2-CORE02 -> site=MEP-96, role=l2
        """
        hostname = self._cfg.hostname or os.path.splitext(self.filename)[0]
        if not self._cfg.hostname:
            self._cfg.hostname = hostname

        parts = hostname.upper().split("-")
        # Find site: typically first two parts MEP + number
        if len(parts) >= 2:
            self._cfg.site = f"{parts[0]}-{parts[1]}"
        else:
            self._cfg.site = hostname

        # Role detection
        hostname_lower = hostname.lower()
        if "l2" in hostname_lower:
            self._cfg.role = "l2"
        elif "l1" in hostname_lower:
            self._cfg.role = "l1"
        elif "core" in hostname_lower:
            self._cfg.role = "core"
        elif "access" in hostname_lower:
            self._cfg.role = "access"
        elif "dist" in hostname_lower:
            self._cfg.role = "distribution"
        else:
            self._cfg.role = "unknown"


# ---------------------------------------------------------------------------
# CLI helper
# ---------------------------------------------------------------------------

def parse_directory(directory: str) -> list[SwitchConfig]:
    """Parse all .txt config files in a directory."""
    import glob
    configs = []
    pattern = os.path.join(directory, "*.txt")
    for filepath in sorted(glob.glob(pattern)):
        print(f"Parsing: {filepath}")
        parser = PicOSParser(filepath)
        try:
            cfg = parser.parse()
            configs.append(cfg)
            print(f"  -> {cfg.hostname}  site={cfg.site}  role={cfg.role}  "
                  f"vlans={len(cfg.vlans)}  interfaces={len(cfg.interfaces)}  "
                  f"bgp_neighbors={len(cfg.bgp.neighbors) if cfg.bgp else 0}")
        except Exception as exc:
            print(f"  ERROR: {exc}")
    return configs


if __name__ == "__main__":
    import sys
    import json

    target = sys.argv[1] if len(sys.argv) > 1 else "."
    configs = parse_directory(target)

    # Simple JSON dump for inspection
    def to_json(obj):
        if hasattr(obj, "__dataclass_fields__"):
            return {k: to_json(getattr(obj, k)) for k in obj.__dataclass_fields__}
        elif isinstance(obj, list):
            return [to_json(x) for x in obj]
        elif isinstance(obj, dict):
            return {k: to_json(v) for k, v in obj.items()}
        return obj

    output = [to_json(c) for c in configs]
    print(json.dumps(output, indent=2, default=str))
