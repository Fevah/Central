"""
Database Loader
================
Loads parsed PicOS configs and Excel guide data into PostgreSQL.

Usage:
    loader = DBLoader(dsn="postgresql://switchbuilder:secret@localhost:5432/switchbuilder")
    loader.connect()
    loader.load_switch_configs(configs)    # list[SwitchConfig]
    loader.load_guide(switches, connections)  # from ExcelImporter
    loader.close()

Or use as a script:
    python db_loader.py --dsn "postgresql://..." --config-dir . --guide switch_guide.xlsx

Dependencies:
    pip install psycopg2-binary openpyxl
"""

from __future__ import annotations

import argparse
import json
import os
import sys
from typing import Optional

# Ensure local parser package is importable when run as script
sys.path.insert(0, os.path.dirname(__file__))

from picos_parser import (
    SwitchConfig, BgpConfig, BgpNeighbor, Vlan, Interface,
    L3Interface, VrrpEntry, DhcpRelay, StaticRoute, SpanningTree,
    ForwardingClass, FirewallFilterEntry, parse_directory
)
from excel_importer import ExcelImporter, GuideSwitch, GuideConnection


# ---------------------------------------------------------------------------
# DBLoader
# ---------------------------------------------------------------------------

class DBLoader:
    def __init__(self, dsn: str):
        self.dsn = dsn
        self._conn = None
        self._cur = None

    # -----------------------------------------------------------------------
    # Connection
    # -----------------------------------------------------------------------

    def connect(self):
        try:
            import psycopg2
            import psycopg2.extras
        except ImportError:
            raise ImportError("psycopg2-binary is required: pip install psycopg2-binary")

        self._psycopg2 = psycopg2
        self._conn = psycopg2.connect(self.dsn)
        self._conn.autocommit = False
        self._cur = self._conn.cursor()
        print(f"Connected to: {self.dsn.split('@')[-1]}")

    def close(self):
        if self._cur:
            self._cur.close()
        if self._conn:
            self._conn.close()

    def commit(self):
        self._conn.commit()

    def rollback(self):
        self._conn.rollback()

    # -----------------------------------------------------------------------
    # Load switch configs
    # -----------------------------------------------------------------------

    def load_switch_configs(self, configs: list[SwitchConfig]):
        """Insert/upsert all parsed switch configurations."""
        for cfg in configs:
            print(f"Loading switch: {cfg.hostname}")
            try:
                switch_id = self._upsert_switch(cfg)
                self._insert_vlans(switch_id, cfg.vlans)
                self._insert_interfaces(switch_id, cfg.interfaces)
                self._insert_l3_interfaces(switch_id, cfg.l3_interfaces)
                if cfg.bgp:
                    bgp_id = self._insert_bgp(switch_id, cfg.bgp)
                    self._insert_bgp_neighbors(bgp_id, cfg.bgp.neighbors)
                    self._insert_bgp_networks(bgp_id, cfg.bgp.networks)
                self._insert_vrrp(switch_id, cfg.vrrp)
                self._insert_dhcp_relay(switch_id, cfg.dhcp_relay)
                self._insert_static_routes(switch_id, cfg.static_routes)
                if cfg.spanning_tree:
                    self._insert_stp(switch_id, cfg.spanning_tree)
                self._insert_cos(switch_id, cfg.cos_forwarding_classes)
                self._insert_firewall_filters(switch_id, cfg.firewall_filters)
                self.commit()
                print(f"  OK: {cfg.hostname}")
            except Exception as exc:
                self.rollback()
                print(f"  ERROR loading {cfg.hostname}: {exc}")
                raise

    # -----------------------------------------------------------------------
    # Load Excel guide
    # -----------------------------------------------------------------------

    def load_guide(
        self,
        switches: list[GuideSwitch],
        connections: list[GuideConnection]
    ):
        """Insert switch guide and connection records."""
        print(f"Loading {len(switches)} guide switches, {len(connections)} connections")
        guide_id_map: dict[str, str] = {}   # switch_name -> guide UUID

        for sw in switches:
            try:
                guide_id = self._upsert_guide_switch(sw)
                if sw.switch_name:
                    guide_id_map[sw.switch_name] = guide_id
                self.commit()
            except Exception as exc:
                self.rollback()
                print(f"  ERROR loading guide switch {sw.switch_name}: {exc}")
                raise

        for conn in connections:
            try:
                guide_id = guide_id_map.get(conn.switch_name) if conn.switch_name else None
                # Look up the switch UUID if it exists in the switches table
                switch_id = self._find_switch_id(conn.switch_name)
                self._insert_connection(guide_id, switch_id, conn)
                self.commit()
            except Exception as exc:
                self.rollback()
                print(f"  ERROR loading connection {conn.switch_name}/{conn.local_port}: {exc}")
                raise

        print("  Guide load complete")

    # -----------------------------------------------------------------------
    # Upsert / insert helpers
    # -----------------------------------------------------------------------

    def _upsert_switch(self, cfg: SwitchConfig) -> str:
        sql = """
            INSERT INTO switches
                (hostname, site, role, management_vrf, inband_enabled, ssh_root_login,
                 loopback_ip, loopback_prefix, source_file)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (hostname) DO UPDATE SET
                site           = EXCLUDED.site,
                role           = EXCLUDED.role,
                management_vrf = EXCLUDED.management_vrf,
                inband_enabled = EXCLUDED.inband_enabled,
                ssh_root_login = EXCLUDED.ssh_root_login,
                loopback_ip    = EXCLUDED.loopback_ip,
                loopback_prefix = EXCLUDED.loopback_prefix,
                source_file    = EXCLUDED.source_file,
                updated_at     = NOW()
            RETURNING id
        """
        self._cur.execute(sql, (
            cfg.hostname, cfg.site, cfg.role,
            cfg.management_vrf, cfg.inband_enabled, cfg.ssh_root_login,
            cfg.loopback_ip, cfg.loopback_prefix, cfg.source_file
        ))
        return self._cur.fetchone()[0]

    def _insert_vlans(self, switch_id: str, vlans: dict):
        # Delete existing and re-insert for clean state
        self._cur.execute("DELETE FROM vlans WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO vlans (switch_id, vlan_id, description, l3_interface)
            VALUES (%s, %s, %s, %s)
            ON CONFLICT (switch_id, vlan_id) DO UPDATE SET
                description  = EXCLUDED.description,
                l3_interface = EXCLUDED.l3_interface
        """
        for vlan in vlans.values():
            self._cur.execute(sql, (switch_id, vlan.vlan_id, vlan.description, vlan.l3_interface))

    def _insert_interfaces(self, switch_id: str, interfaces: dict):
        self._cur.execute("DELETE FROM interfaces WHERE switch_id = %s", (switch_id,))
        iface_sql = """
            INSERT INTO interfaces
                (switch_id, interface_name, description, speed, mtu, fec, breakout,
                 native_vlan_id, port_mode, vlan_members, aggregate_member)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT (switch_id, interface_name) DO UPDATE SET
                description     = EXCLUDED.description,
                speed           = EXCLUDED.speed,
                mtu             = EXCLUDED.mtu,
                fec             = EXCLUDED.fec,
                breakout        = EXCLUDED.breakout,
                native_vlan_id  = EXCLUDED.native_vlan_id,
                port_mode       = EXCLUDED.port_mode,
                vlan_members    = EXCLUDED.vlan_members,
                aggregate_member = EXCLUDED.aggregate_member
            RETURNING id
        """
        voice_sql = """
            INSERT INTO interface_voice_vlans (interface_id, vlan_id, mode, tagged_mode)
            VALUES (%s, %s, %s, %s)
        """
        for iface in interfaces.values():
            self._cur.execute(iface_sql, (
                switch_id, iface.interface_name, iface.description,
                iface.speed, iface.mtu, iface.fec, iface.breakout,
                iface.native_vlan_id, iface.port_mode, iface.vlan_members,
                iface.aggregate_member
            ))
            iface_id = self._cur.fetchone()[0]
            for vv in iface.voice_vlans:
                self._cur.execute(voice_sql, (
                    iface_id,
                    vv.get("vlan_id"),
                    vv.get("mode"),
                    vv.get("tagged_mode"),
                ))

    def _insert_l3_interfaces(self, switch_id: str, l3_interfaces: dict):
        self._cur.execute("DELETE FROM l3_interfaces WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO l3_interfaces
                (switch_id, interface_name, ip_address, prefix_length, description)
            VALUES (%s, %s, %s, %s, %s)
            ON CONFLICT (switch_id, interface_name) DO UPDATE SET
                ip_address    = EXCLUDED.ip_address,
                prefix_length = EXCLUDED.prefix_length,
                description   = EXCLUDED.description
        """
        for l3 in l3_interfaces.values():
            self._cur.execute(sql, (
                switch_id, l3.interface_name, l3.ip_address,
                l3.prefix_length, l3.description
            ))

    def _insert_bgp(self, switch_id: str, bgp: BgpConfig) -> str:
        sql = """
            INSERT INTO bgp_config
                (switch_id, local_as, router_id, ebgp_requires_policy, max_paths)
            VALUES (%s, %s, %s, %s, %s)
            ON CONFLICT (switch_id) DO UPDATE SET
                local_as             = EXCLUDED.local_as,
                router_id            = EXCLUDED.router_id,
                ebgp_requires_policy = EXCLUDED.ebgp_requires_policy,
                max_paths            = EXCLUDED.max_paths
            RETURNING id
        """
        self._cur.execute(sql, (
            switch_id, bgp.local_as, bgp.router_id,
            bgp.ebgp_requires_policy, bgp.max_paths
        ))
        return self._cur.fetchone()[0]

    def _insert_bgp_neighbors(self, bgp_id: str, neighbors: list):
        self._cur.execute("DELETE FROM bgp_neighbors WHERE bgp_id = %s", (bgp_id,))
        sql = """
            INSERT INTO bgp_neighbors
                (bgp_id, neighbor_ip, remote_as, description, bfd_enabled, ipv4_unicast)
            VALUES (%s, %s, %s, %s, %s, %s)
        """
        for n in neighbors:
            self._cur.execute(sql, (
                bgp_id, n.neighbor_ip, n.remote_as,
                n.description, n.bfd_enabled, n.ipv4_unicast
            ))

    def _insert_bgp_networks(self, bgp_id: str, networks: list):
        self._cur.execute("DELETE FROM bgp_networks WHERE bgp_id = %s", (bgp_id,))
        sql = "INSERT INTO bgp_networks (bgp_id, network_prefix) VALUES (%s, %s)"
        for net in networks:
            self._cur.execute(sql, (bgp_id, net))

    def _insert_vrrp(self, switch_id: str, vrrp_list: list):
        self._cur.execute("DELETE FROM vrrp_config WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO vrrp_config
                (switch_id, interface_name, vrid, virtual_ip, load_balance)
            VALUES (%s, %s, %s, %s, %s)
        """
        for v in vrrp_list:
            self._cur.execute(sql, (
                switch_id, v.interface_name, v.vrid, v.virtual_ip, v.load_balance
            ))

    def _insert_dhcp_relay(self, switch_id: str, relays: list):
        self._cur.execute("DELETE FROM dhcp_relay WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO dhcp_relay
                (switch_id, interface_name, dhcp_server_address, relay_agent_address, disabled)
            VALUES (%s, %s, %s, %s, %s)
        """
        for relay in relays:
            for server in relay.dhcp_server_addresses:
                self._cur.execute(sql, (
                    switch_id, relay.interface_name,
                    server, relay.relay_agent_address, relay.disabled
                ))

    def _insert_static_routes(self, switch_id: str, routes: list):
        self._cur.execute("DELETE FROM static_routes WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO static_routes (switch_id, prefix, next_hop) VALUES (%s, %s, %s)
            ON CONFLICT (switch_id, prefix, next_hop) DO NOTHING
        """
        for r in routes:
            self._cur.execute(sql, (switch_id, r.prefix, r.next_hop))

    def _insert_stp(self, switch_id: str, stp: SpanningTree):
        sql = """
            INSERT INTO spanning_tree (switch_id, protocol, bridge_priority)
            VALUES (%s, %s, %s)
            ON CONFLICT (switch_id) DO UPDATE SET
                protocol       = EXCLUDED.protocol,
                bridge_priority = EXCLUDED.bridge_priority
        """
        self._cur.execute(sql, (switch_id, stp.protocol, stp.bridge_priority))

    def _insert_cos(self, switch_id: str, classes: list):
        self._cur.execute("DELETE FROM cos_forwarding_classes WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO cos_forwarding_classes (switch_id, class_name, local_priority)
            VALUES (%s, %s, %s)
        """
        for fc in classes:
            self._cur.execute(sql, (switch_id, fc.class_name, fc.local_priority))

    def _insert_firewall_filters(self, switch_id: str, filters: list):
        self._cur.execute("DELETE FROM firewall_filters WHERE switch_id = %s", (switch_id,))
        sql = """
            INSERT INTO firewall_filters
                (switch_id, filter_name, sequence, from_params, then_params, input_interface)
            VALUES (%s, %s, %s, %s, %s, %s)
        """
        for f in filters:
            self._cur.execute(sql, (
                switch_id, f.filter_name, f.sequence,
                json.dumps(f.from_params), json.dumps(f.then_params),
                f.input_interface
            ))

    def _upsert_guide_switch(self, sw: GuideSwitch) -> str:
        sql = """
            INSERT INTO switch_guide
                (switch_name, site, building, floor, rack, model, serial_number,
                 management_ip, uplink_switch, uplink_port, notes, enabled, raw_data)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            ON CONFLICT DO NOTHING
            RETURNING id
        """
        raw_json = json.dumps(
            {str(k): str(v) for k, v in sw.raw_data.items() if v is not None},
            default=str
        )
        self._cur.execute(sql, (
            sw.switch_name, sw.site, sw.building, sw.floor, sw.rack,
            sw.model, sw.serial_number, sw.management_ip,
            sw.uplink_switch, sw.uplink_port, sw.notes, sw.enabled,
            raw_json
        ))
        row = self._cur.fetchone()
        if row:
            return row[0]
        # Already existed — fetch existing id
        self._cur.execute(
            "SELECT id FROM switch_guide WHERE switch_name = %s AND (management_ip = %s OR management_ip IS NULL)",
            (sw.switch_name, sw.management_ip)
        )
        row = self._cur.fetchone()
        return row[0] if row else None

    def _insert_connection(
        self,
        guide_id: Optional[str],
        switch_id: Optional[str],
        conn: GuideConnection
    ):
        sql = """
            INSERT INTO switch_connections
                (guide_id, switch_id, local_port, remote_device, remote_port,
                 connection_type, vlan_id, voice_vlan_id, vlan_members,
                 speed, description, enabled, notes)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
        """
        self._cur.execute(sql, (
            guide_id, switch_id,
            conn.local_port, conn.remote_device, conn.remote_port,
            conn.connection_type,
            getattr(conn, 'vlan_id', None),
            getattr(conn, 'voice_vlan_id', None),
            getattr(conn, 'vlan_members', None),
            getattr(conn, 'speed', None),
            getattr(conn, 'description', None),
            conn.enabled, conn.notes
        ))

    def _find_switch_id(self, hostname: Optional[str]) -> Optional[str]:
        if not hostname:
            return None
        self._cur.execute(
            "SELECT id FROM switches WHERE hostname = %s", (hostname,)
        )
        row = self._cur.fetchone()
        return row[0] if row else None


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Load FS/PicOS switch configs and guide into PostgreSQL"
    )
    parser.add_argument(
        "--dsn",
        default=os.environ.get(
            "SWITCHBUILDER_DSN",
            "postgresql://switchbuilder:switchbuilder@localhost:5432/switchbuilder"
        ),
        help="PostgreSQL connection string"
    )
    parser.add_argument(
        "--config-dir",
        default=".",
        help="Directory containing .txt PicOS config files"
    )
    parser.add_argument(
        "--guide",
        default="switch_guide.xlsx",
        help="Path to switch_guide.xlsx"
    )
    parser.add_argument(
        "--configs-only", action="store_true",
        help="Only load config files, skip Excel guide"
    )
    parser.add_argument(
        "--guide-only", action="store_true",
        help="Only load Excel guide, skip config files"
    )

    args = parser.parse_args()

    loader = DBLoader(dsn=args.dsn)
    loader.connect()

    try:
        if not args.guide_only:
            configs = parse_directory(args.config_dir)
            if configs:
                loader.load_switch_configs(configs)
            else:
                print(f"No .txt config files found in: {args.config_dir}")

        if not args.configs_only and os.path.exists(args.guide):
            importer = ExcelImporter(args.guide)
            switches, connections = importer.load()
            loader.load_guide(switches, connections)
        elif not args.configs_only:
            print(f"Guide file not found: {args.guide} (skipping)")

        print("All done.")

    finally:
        loader.close()


if __name__ == "__main__":
    main()
