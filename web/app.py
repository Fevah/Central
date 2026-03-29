"""
Central Web App
======================
FastAPI application for building FS/PicOS switch configurations from scratch.

The database is the single source of truth.  The switch_guide (Excel) drives
port-level interface configuration.  Exporting produces a clean .txt file
ready to paste directly into a PicOS switch.

Routes:
  GET  /                              Dashboard
  GET  /switches/new                  New switch form
  POST /switches/new                  Create switch
  GET  /switches/{hostname}           Switch detail
  GET  /switches/{hostname}/edit      Edit switch
  POST /switches/{hostname}/edit      Save switch edits
  POST /switches/{hostname}/delete    Delete switch

  POST /switches/{hostname}/vlans/add            Add VLAN
  POST /switches/{hostname}/vlans/{id}/delete    Delete VLAN

  POST /switches/{hostname}/interfaces/add             Add interface
  POST /switches/{hostname}/interfaces/{id}/edit       Edit interface
  POST /switches/{hostname}/interfaces/{id}/delete     Delete interface

  POST /switches/{hostname}/sync-guide    Sync enabled connections → interfaces
  GET  /switches/{hostname}/export        Download clean .txt config
  GET  /switches/{hostname}/preview       Preview generated config

  GET  /guide                         Switch guide
  GET  /guide/{id}                    Guide detail + connections
  POST /guide/toggle/{conn_id}        Toggle connection enabled (HTMX)
  POST /guide/{id}/edit-connection/{conn_id}  Edit connection fields (HTMX)

  GET  /import                        Import page
  POST /import/configs                Upload .txt files
  POST /import/guide                  Upload .xlsx guide
"""

from __future__ import annotations

import io
import json
import os
import sys
import tempfile
from contextlib import asynccontextmanager
from typing import Optional

import base64
import secrets

import psycopg2
import psycopg2.extras
import psycopg2.pool
from fastapi import FastAPI, Request, Form, UploadFile, File, HTTPException
from fastapi.responses import HTMLResponse, JSONResponse, PlainTextResponse, Response, RedirectResponse
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates

sys.path.insert(0, os.path.dirname(os.path.dirname(__file__)))
from parser.picos_parser import PicOSParser
from parser.excel_importer import ExcelImporter
from parser.db_loader import DBLoader

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

DSN = os.environ.get(
    "CENTRAL_DSN",
    "postgresql://central:central@localhost:5432/central"
)

BASE_DIR   = os.path.dirname(__file__)
STATIC_DIR = os.path.join(BASE_DIR, "static")
TMPL_DIR   = os.path.join(BASE_DIR, "templates")

# ---------------------------------------------------------------------------
# HTTP Basic Auth (middleware — applies to every request)
# ---------------------------------------------------------------------------

_AUTH_USER = os.environ.get("CENTRAL_USER", "admin").encode()
_AUTH_PASS = os.environ.get("CENTRAL_PASS", "admin").encode()
_REALM     = b'Basic realm="Central"'
_UNAUTH    = Response(
    content="Unauthorized",
    status_code=401,
    headers={"WWW-Authenticate": 'Basic realm="Central"'},
)


async def _basic_auth_middleware(request: Request, call_next):
    # Skip auth for static assets so CSS/JS load inside the browser challenge
    if request.url.path.startswith("/static/"):
        return await call_next(request)
    auth = request.headers.get("Authorization", "")
    if not auth.startswith("Basic "):
        return _UNAUTH
    try:
        decoded = base64.b64decode(auth[6:]).split(b":", 1)
        user, pwd = decoded[0], decoded[1]
    except Exception:
        return _UNAUTH
    ok = secrets.compare_digest(user, _AUTH_USER) and secrets.compare_digest(pwd, _AUTH_PASS)
    if not ok:
        return _UNAUTH
    return await call_next(request)


# ---------------------------------------------------------------------------
# DB connection pool — reuse connections to avoid 2s WSL2→Podman overhead
# ---------------------------------------------------------------------------

_pool: Optional["psycopg2.pool.ThreadedConnectionPool"] = None


def _init_pool():
    global _pool
    _pool = psycopg2.pool.ThreadedConnectionPool(
        minconn=2, maxconn=10,
        dsn=DSN,
        connect_timeout=10,
    )
    # Apply session settings on each new connection
    for _ in range(_pool.minconn):
        conn = _pool.getconn()
        conn.autocommit = True
        with conn.cursor() as cur:
            cur.execute("SET statement_timeout = '10000'")
        _pool.putconn(conn)


def get_conn():
    if _pool is None:
        _init_pool()
    conn = _pool.getconn()
    conn.autocommit = True
    return conn


def release_conn(conn):
    if _pool and conn:
        _pool.putconn(conn)


def query(sql: str, params=None) -> list[dict]:
    conn = get_conn()
    try:
        with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cur:
            cur.execute(sql, params or ())
            return [dict(r) for r in cur.fetchall()]
    finally:
        release_conn(conn)


def query_one(sql: str, params=None) -> Optional[dict]:
    rows = query(sql, params)
    return rows[0] if rows else None


def execute(sql: str, params=None) -> Optional[str]:
    """Execute and return first column of first row if any (for RETURNING id)."""
    conn = get_conn()
    try:
        with conn.cursor() as cur:
            cur.execute(sql, params or ())
            try:
                row = cur.fetchone()
                return row[0] if row else None
            except Exception:
                return None
    finally:
        release_conn(conn)


# ---------------------------------------------------------------------------
# Config generator — produces clean PicOS set-commands, no comments
# ---------------------------------------------------------------------------

def build_config(hostname: str) -> str:
    """
    Generate clean PicOS set-commands from database.
    Only access ports that are enabled in switch_connections are included.
    Trunk/uplink/infrastructure ports are always included.
    Output has no blank lines or comments — ready to paste into the switch.
    """
    switch = query_one("SELECT * FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        return ""

    sid = switch["id"]
    lines: list[str] = []

    def s(*parts):
        lines.append("set " + " ".join(str(p) for p in parts))

    # ---- System ----
    s("system hostname", f'"{hostname}"')
    if switch.get("management_vrf"):
        s("system management-vrf enable true")
    if switch.get("inband_enabled"):
        s("system inband enable true")
    if switch.get("ssh_root_login"):
        s("system services ssh root-login", switch["ssh_root_login"])

    # ---- IP routing ----
    s("ip routing enable true")

    # ---- CoS forwarding classes ----
    cos = query(
        "SELECT * FROM cos_forwarding_classes WHERE switch_id = %s ORDER BY local_priority DESC NULLS LAST, class_name",
        (sid,)
    )
    for fc in cos:
        if fc["local_priority"] is not None:
            s("class-of-service forwarding-class", fc["class_name"], "local-priority", fc["local_priority"])
        else:
            s("class-of-service forwarding-class", fc["class_name"])

    # ---- Firewall filters ----
    fw = query(
        "SELECT * FROM firewall_filters WHERE switch_id = %s ORDER BY filter_name, sequence",
        (sid,)
    )
    # Group by filter_name to avoid duplicate input-interface lines
    fw_input_done: set = set()
    for f in fw:
        fp = f.get("from_params") or {}
        tp = f.get("then_params") or {}
        for k, v in tp.items():
            s("firewall filter", f["filter_name"], "sequence", f["sequence"], "then", k, v)
        for k, v in fp.items():
            s("firewall filter", f["filter_name"], "sequence", f["sequence"], "from", k, v)
        if f.get("input_interface") and f["filter_name"] not in fw_input_done:
            s("firewall filter", f["filter_name"], "input vlan-interface", f["input_interface"])
            fw_input_done.add(f["filter_name"])

    # ---- Interfaces ----
    # Determine which ports are enabled in the guide
    enabled_ports: set[str] = set()
    guide_conns = query(
        "SELECT local_port, connection_type FROM switch_connections WHERE switch_id = %s AND enabled = true AND local_port IS NOT NULL",
        (sid,)
    )
    for c in guide_conns:
        if c["local_port"]:
            enabled_ports.add(c["local_port"])

    interfaces = query(
        "SELECT i.*, array_agg(row_to_json(vv)) FILTER (WHERE vv.id IS NOT NULL) AS voice_vlans_json "
        "FROM interfaces i "
        "LEFT JOIN interface_voice_vlans vv ON vv.interface_id = i.id "
        "WHERE i.switch_id = %s "
        "GROUP BY i.id "
        "ORDER BY i.interface_name",
        (sid,)
    )
    # ECMP if any interfaces use it
    has_ecmp = any(i.get("vlan_members") and "ecmp" in (i.get("description") or "").lower() for i in interfaces)

    for iface in interfaces:
        name = iface["interface_name"]

        # Breakout declarations first
        if iface.get("breakout"):
            s("interface gigabit-ethernet", name, "breakout true")
            continue

        # Skip access ports not enabled in guide (if guide data exists)
        is_access = not iface.get("port_mode") or iface["port_mode"] == "access"
        if is_access and enabled_ports and name not in enabled_ports:
            continue

        if iface.get("description"):
            s("interface gigabit-ethernet", name, "description", f'"{iface["description"]}"')
        if iface.get("mtu"):
            s("interface gigabit-ethernet", name, "mtu", iface["mtu"])
        if iface.get("fec"):
            s("interface gigabit-ethernet", name, "fec true")
        if iface.get("native_vlan_id"):
            s("interface gigabit-ethernet", name, "family ethernet-switching native-vlan-id", iface["native_vlan_id"])
        if iface.get("port_mode"):
            s("interface gigabit-ethernet", name, "family ethernet-switching port-mode", f'"{iface["port_mode"]}"')
        if iface.get("vlan_members"):
            s("interface gigabit-ethernet", name, "family ethernet-switching vlan members", iface["vlan_members"])
        if iface.get("speed"):
            s("interface gigabit-ethernet", name, "speed", f'"{iface["speed"]}"')

        # Voice VLANs
        vv_json = iface.get("voice_vlans_json") or []
        for vv in vv_json:
            if isinstance(vv, dict):
                if vv.get("vlan_id"):
                    s("interface gigabit-ethernet", name, "voice-vlan vlan-id", vv["vlan_id"])
                if vv.get("mode"):
                    s("interface gigabit-ethernet", name, "voice-vlan mode", f'"{vv["mode"]}"')
                if vv.get("tagged_mode"):
                    s("interface gigabit-ethernet", name, "voice-vlan tagged mode", f'"{vv["tagged_mode"]}"')

    # ---- L3 interfaces ----
    l3_ifaces = query(
        "SELECT * FROM l3_interfaces WHERE switch_id = %s ORDER BY interface_name",
        (sid,)
    )
    for l3 in l3_ifaces:
        iface_type = "loopback" if l3["interface_name"] == "lo0" else "vlan-interface"
        if l3.get("description"):
            s("l3-interface", iface_type, l3["interface_name"], "description", f'"{l3["description"]}"')
        if l3.get("ip_address"):
            s("l3-interface", iface_type, l3["interface_name"], "address", l3["ip_address"], "prefix-length", l3["prefix_length"])
        else:
            s("l3-interface", iface_type, l3["interface_name"])

    # ---- BGP ----
    bgp = query_one("SELECT * FROM bgp_config WHERE switch_id = %s", (sid,))
    if bgp:
        s("protocols bgp local-as", f'"{bgp["local_as"]}"')
        if bgp.get("ebgp_requires_policy") is False:
            s("protocols bgp ebgp-requires-policy false")
        if bgp.get("router_id"):
            s("protocols bgp router-id", bgp["router_id"])
        neighbors = query("SELECT * FROM bgp_neighbors WHERE bgp_id = %s ORDER BY neighbor_ip", (bgp["id"],))
        for n in neighbors:
            s("protocols bgp neighbor", n["neighbor_ip"], "remote-as", f'"{n["remote_as"]}"')
            if n.get("bfd_enabled"):
                s("protocols bgp neighbor", n["neighbor_ip"], "bfd")
            if n.get("description"):
                s("protocols bgp neighbor", n["neighbor_ip"], "description", f'"{n["description"]}"')
            if n.get("ipv4_unicast"):
                s("protocols bgp neighbor", n["neighbor_ip"], "ipv4-unicast")
        networks = query("SELECT * FROM bgp_networks WHERE bgp_id = %s ORDER BY network_prefix", (bgp["id"],))
        for net in networks:
            s("protocols bgp ipv4-unicast network", net["network_prefix"])
        if bgp.get("redistribute_connected", True):
            s("protocols bgp ipv4-unicast redistribute connected")
        s("protocols bgp ipv4-unicast multipath ebgp maximum-paths", bgp.get("max_paths", 4))

    # ---- BFD ----
    has_bfd = query_one(
        "SELECT 1 FROM bgp_neighbors bn JOIN bgp_config b ON b.id = bn.bgp_id WHERE b.switch_id = %s AND bn.bfd_enabled = true LIMIT 1",
        (sid,)
    )
    if has_bfd:
        s("protocols bfd")

    # ---- DHCP relay ----
    dhcp = query(
        "SELECT * FROM dhcp_relay WHERE switch_id = %s ORDER BY interface_name, dhcp_server_address",
        (sid,)
    )
    for d in dhcp:
        s("protocols dhcp relay interface", d["interface_name"], "disable", str(d.get("disabled", False)).lower())
        s("protocols dhcp relay interface", d["interface_name"], "dhcp-server-address", d["dhcp_server_address"])
        if d.get("relay_agent_address"):
            s("protocols dhcp relay interface", d["interface_name"], "relay-agent-address", d["relay_agent_address"])

    # ---- LLDP ----
    s("protocols lldp enable true")

    # ---- STP ----
    stp = query_one("SELECT * FROM spanning_tree WHERE switch_id = %s", (sid,))
    if stp:
        s("protocols spanning-tree", stp["protocol"], "bridge-priority", stp["bridge_priority"])

    # ---- Static routes ----
    routes = query("SELECT * FROM static_routes WHERE switch_id = %s ORDER BY prefix", (sid,))
    for r in routes:
        s("protocols static route", r["prefix"], "next-hop", r["next_hop"])

    # ---- VRRP ----
    vrrp = query(
        "SELECT * FROM vrrp_config WHERE switch_id = %s ORDER BY interface_name, vrid",
        (sid,)
    )
    for v in vrrp:
        s("protocols vrrp interface", v["interface_name"], "vrid", v["vrid"], "ip", v["virtual_ip"])
        if v.get("load_balance"):
            s("protocols vrrp interface", v["interface_name"], "vrid", v["vrid"], "load-balance disable false")

    # ---- VLANs ----
    vlans = query("SELECT * FROM vlans WHERE switch_id = %s ORDER BY vlan_id", (sid,))
    for v in vlans:
        if v.get("description"):
            s("vlans vlan-id", v["vlan_id"], "description", f'"{v["description"]}"')
        else:
            s("vlans vlan-id", v["vlan_id"])
        if v.get("l3_interface"):
            s("vlans vlan-id", v["vlan_id"], "l3-interface", f'"{v["l3_interface"]}"')

    # Update last_exported_at
    execute("UPDATE switches SET last_exported_at = NOW() WHERE id = %s", (sid,))

    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# Guide → Interfaces sync
# ---------------------------------------------------------------------------

def sync_guide_to_interfaces(switch_id: str) -> dict:
    """
    For each enabled switch_connection, create or update the interfaces record.
    Returns {"created": N, "updated": N, "skipped": N}
    """
    conns = query(
        """SELECT * FROM switch_connections
           WHERE switch_id = %s AND enabled = true AND local_port IS NOT NULL
           ORDER BY local_port""",
        (switch_id,)
    )
    created = updated = skipped = 0

    for conn in conns:
        port = conn["local_port"]
        ctype = (conn.get("connection_type") or "access").lower()
        desc = conn.get("description") or conn.get("remote_device")
        vlan_id = conn.get("vlan_id")
        voice_vlan_id = conn.get("voice_vlan_id")
        vlan_members = conn.get("vlan_members")
        speed = conn.get("speed")

        # Determine port_mode
        if ctype in ("trunk", "uplink"):
            port_mode = "trunk"
        else:
            port_mode = None  # access port — no port-mode set command

        # Upsert interface
        existing = query_one(
            "SELECT id FROM interfaces WHERE switch_id = %s AND interface_name = %s",
            (switch_id, port)
        )
        if existing:
            execute(
                """UPDATE interfaces SET
                    description    = COALESCE(%s, description),
                    native_vlan_id = COALESCE(%s, native_vlan_id),
                    port_mode      = COALESCE(%s, port_mode),
                    vlan_members   = COALESCE(%s, vlan_members),
                    speed          = COALESCE(%s, speed)
                   WHERE id = %s""",
                (desc, vlan_id, port_mode, vlan_members, speed, existing["id"])
            )
            iface_id = existing["id"]
            updated += 1
        else:
            iface_id = execute(
                """INSERT INTO interfaces
                    (switch_id, interface_name, description, native_vlan_id,
                     port_mode, vlan_members, speed)
                   VALUES (%s, %s, %s, %s, %s, %s, %s)
                   RETURNING id""",
                (switch_id, port, desc, vlan_id, port_mode, vlan_members, speed)
            )
            created += 1

        # Voice VLAN
        if voice_vlan_id and iface_id:
            existing_vv = query_one(
                "SELECT id FROM interface_voice_vlans WHERE interface_id = %s", (iface_id,)
            )
            if not existing_vv:
                execute(
                    "INSERT INTO interface_voice_vlans (interface_id, vlan_id, mode, tagged_mode) VALUES (%s, %s, 'manual', 'tag')",
                    (iface_id, voice_vlan_id)
                )

    return {"created": created, "updated": updated, "skipped": skipped}


# ---------------------------------------------------------------------------
# FastAPI app
# ---------------------------------------------------------------------------

@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        _init_pool()
        query_one("SELECT 1")
        print(f"DB: {DSN.split('@')[-1]}")
    except Exception as e:
        print(f"WARNING: DB not available: {e}")
    yield
    if _pool:
        _pool.closeall()

app = FastAPI(title="Central", lifespan=lifespan)
app.middleware("http")(_basic_auth_middleware)
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")
templates = Jinja2Templates(directory=TMPL_DIR)

def tmpl(name: str, request: Request, **ctx):
    return templates.TemplateResponse(name, {"request": request, **ctx})


# ---------------------------------------------------------------------------
# Dashboard
# ---------------------------------------------------------------------------

@app.get("/", response_class=HTMLResponse)
async def dashboard(request: Request):
    try:
        switches = query("""
            SELECT s.hostname, s.site, s.role, s.loopback_ip, s.last_exported_at,
                b.local_as,
                COUNT(DISTINCT v.id)  AS vlan_count,
                COUNT(DISTINCT i.id)  AS iface_count,
                COUNT(DISTINCT bn.id) AS bgp_neighbor_count,
                (SELECT COUNT(*) FROM switch_connections sc WHERE sc.switch_id = s.id AND sc.enabled) AS enabled_ports
            FROM switches s
            LEFT JOIN bgp_config b      ON b.switch_id = s.id
            LEFT JOIN vlans v           ON v.switch_id = s.id
            LEFT JOIN interfaces i      ON i.switch_id = s.id
            LEFT JOIN bgp_neighbors bn  ON bn.bgp_id = b.id
            GROUP BY s.id, s.hostname, s.site, s.role, s.loopback_ip, s.last_exported_at, b.local_as
            ORDER BY s.site, s.hostname
        """)
        db_ok = True
    except Exception:
        switches = []
        db_ok = False
    return tmpl("dashboard.html", request, switches=switches, db_ok=db_ok)


# ---------------------------------------------------------------------------
# Switch CRUD
# ---------------------------------------------------------------------------

@app.get("/switches/new", response_class=HTMLResponse)
async def switch_new_form(request: Request):
    return tmpl("switch_form.html", request, switch=None, action="/switches/new", title="New Switch")

@app.post("/switches/new", response_class=HTMLResponse)
async def switch_create(
    request: Request,
    hostname: str = Form(...),
    site: str = Form(""),
    role: str = Form("core"),
    loopback_ip: str = Form(""),
    loopback_prefix: str = Form("32"),
    management_vrf: str = Form("off"),
    inband_enabled: str = Form("off"),
    ssh_root_login: str = Form("allow"),
    picos_version: str = Form("4.6"),
):
    hostname = hostname.strip().upper()
    try:
        execute(
            """INSERT INTO switches
                (hostname, site, role, loopback_ip, loopback_prefix,
                 management_vrf, inband_enabled, ssh_root_login, picos_version)
               VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s)""",
            (
                hostname, site.strip() or None, role,
                loopback_ip.strip() or None,
                int(loopback_prefix) if loopback_prefix.strip() else None,
                management_vrf == "on",
                inband_enabled == "on",
                ssh_root_login,
                picos_version,
            )
        )
    except Exception as e:
        return tmpl("switch_form.html", request, switch=None, action="/switches/new",
                    title="New Switch", error=str(e))
    return RedirectResponse(f"/switches/{hostname}", status_code=303)


@app.get("/switches/{hostname}", response_class=HTMLResponse)
async def switch_detail(request: Request, hostname: str):
    try:
        switch = query_one("SELECT * FROM switches WHERE hostname = %s", (hostname,))
        if not switch:
            raise HTTPException(404, f"Switch not found: {hostname}")
        sid = switch["id"]

        vlans         = query("SELECT * FROM vlans WHERE switch_id = %s ORDER BY vlan_id", (sid,))
        interfaces    = query("SELECT * FROM interfaces WHERE switch_id = %s ORDER BY interface_name", (sid,))
        l3_ifaces     = query("SELECT * FROM l3_interfaces WHERE switch_id = %s ORDER BY interface_name", (sid,))
        bgp           = query_one("SELECT * FROM bgp_config WHERE switch_id = %s", (sid,))
        bgp_neighbors = query("SELECT * FROM bgp_neighbors WHERE bgp_id = %s", (bgp["id"],)) if bgp else []
        bgp_networks  = query("SELECT * FROM bgp_networks WHERE bgp_id = %s ORDER BY network_prefix", (bgp["id"],)) if bgp else []
        vrrp          = query("SELECT * FROM vrrp_config WHERE switch_id = %s ORDER BY interface_name, vrid", (sid,))
        static_routes = query("SELECT * FROM static_routes WHERE switch_id = %s ORDER BY prefix", (sid,))
        dhcp          = query("SELECT * FROM dhcp_relay WHERE switch_id = %s ORDER BY interface_name", (sid,))
        stp           = query_one("SELECT * FROM spanning_tree WHERE switch_id = %s", (sid,))
        cos           = query("SELECT * FROM cos_forwarding_classes WHERE switch_id = %s ORDER BY local_priority DESC NULLS LAST", (sid,))
        guide_connections = query(
            """SELECT sc.*, sg.switch_name as guide_switch_name
               FROM switch_connections sc
               LEFT JOIN switch_guide sg ON sg.id = sc.guide_id
               WHERE sc.switch_id = %s ORDER BY sc.local_port""",
            (sid,)
        )
    except HTTPException:
        raise
    except Exception as e:
        return HTMLResponse(
            f"""<!DOCTYPE html><html><head><title>Error — Central</title>
            <link rel="stylesheet" href="/static/css/app.css"></head>
            <body><main class="container" style="padding-top:3rem">
            <div class="alert alert-error">
              <strong>Error loading {hostname}:</strong> {e}<br>
              <a href="/" style="color:inherit;margin-top:0.5rem;display:inline-block">← Dashboard</a>
            </div></main></body></html>""",
            status_code=500,
        )

    return tmpl("switch_detail.html", request,
        switch=switch, vlans=vlans, interfaces=interfaces,
        l3_ifaces=l3_ifaces, bgp=bgp, bgp_neighbors=bgp_neighbors,
        bgp_networks=bgp_networks, vrrp=vrrp, static_routes=static_routes,
        dhcp=dhcp, stp=stp, cos=cos, guide_connections=guide_connections)


@app.get("/switches/{hostname}/edit", response_class=HTMLResponse)
async def switch_edit_form(request: Request, hostname: str):
    switch = query_one("SELECT * FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    return tmpl("switch_form.html", request, switch=switch,
                action=f"/switches/{hostname}/edit", title=f"Edit {hostname}")

@app.post("/switches/{hostname}/edit", response_class=HTMLResponse)
async def switch_edit(
    request: Request, hostname: str,
    site: str = Form(""),
    role: str = Form("core"),
    loopback_ip: str = Form(""),
    loopback_prefix: str = Form("32"),
    management_vrf: str = Form("off"),
    inband_enabled: str = Form("off"),
    ssh_root_login: str = Form("allow"),
    picos_version: str = Form("4.6"),
    management_ip: str = Form(""),
    ssh_username: str = Form("root"),
    ssh_port: str = Form("22"),
    ssh_password: str = Form(""),
):
    # Build update, only overwrite ssh_password if a new one was provided
    pwd_clause = ", ssh_password=%s" if ssh_password.strip() else ""
    params = [
        site or None, role,
        loopback_ip or None,
        int(loopback_prefix) if loopback_prefix.strip() else None,
        management_vrf == "on", inband_enabled == "on",
        ssh_root_login, picos_version,
        management_ip.strip() or None,
        ssh_username.strip() or "root",
        int(ssh_port) if ssh_port.strip().isdigit() else 22,
    ]
    if ssh_password.strip():
        params.append(ssh_password)
    params.append(hostname)

    execute(
        f"""UPDATE switches SET site=%s, role=%s, loopback_ip=%s, loopback_prefix=%s,
            management_vrf=%s, inband_enabled=%s, ssh_root_login=%s, picos_version=%s,
            management_ip=%s, ssh_username=%s, ssh_port=%s{pwd_clause},
            updated_at=NOW()
           WHERE hostname=%s""",
        params
    )
    return RedirectResponse(f"/switches/{hostname}", status_code=303)


@app.post("/switches/{hostname}/delete")
async def switch_delete(hostname: str):
    execute("DELETE FROM switches WHERE hostname = %s", (hostname,))
    return RedirectResponse("/", status_code=303)


# ---- VLANs ----

@app.post("/switches/{hostname}/vlans/add", response_class=HTMLResponse)
async def vlan_add(
    request: Request, hostname: str,
    vlan_id: int = Form(...),
    description: str = Form(""),
    l3_interface: str = Form(""),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO vlans (switch_id, vlan_id, description, l3_interface)
           VALUES (%s, %s, %s, %s)
           ON CONFLICT (switch_id, vlan_id) DO UPDATE
           SET description=%s, l3_interface=%s""",
        (switch["id"], vlan_id,
         description or None, l3_interface or None,
         description or None, l3_interface or None)
    )
    return RedirectResponse(f"/switches/{hostname}#vlans", status_code=303)


@app.post("/switches/{hostname}/vlans/{vlan_uuid}/delete")
async def vlan_delete(hostname: str, vlan_uuid: str):
    execute("DELETE FROM vlans WHERE id = %s", (vlan_uuid,))
    return RedirectResponse(f"/switches/{hostname}#vlans", status_code=303)


# ---- Interfaces ----

@app.post("/switches/{hostname}/interfaces/add", response_class=HTMLResponse)
async def interface_add(
    request: Request, hostname: str,
    interface_name: str = Form(...),
    description: str = Form(""),
    speed: str = Form(""),
    native_vlan_id: str = Form(""),
    port_mode: str = Form(""),
    vlan_members: str = Form(""),
    fec: str = Form("off"),
    breakout: str = Form("off"),
    mtu: str = Form(""),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO interfaces
            (switch_id, interface_name, description, speed, native_vlan_id,
             port_mode, vlan_members, fec, breakout, mtu)
           VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
           ON CONFLICT (switch_id, interface_name) DO UPDATE SET
            description    = EXCLUDED.description,
            speed          = EXCLUDED.speed,
            native_vlan_id = EXCLUDED.native_vlan_id,
            port_mode      = EXCLUDED.port_mode,
            vlan_members   = EXCLUDED.vlan_members,
            fec            = EXCLUDED.fec,
            breakout       = EXCLUDED.breakout,
            mtu            = EXCLUDED.mtu""",
        (
            switch["id"], interface_name.strip(),
            description or None, speed or None,
            int(native_vlan_id) if native_vlan_id.strip() else None,
            port_mode or None, vlan_members or None,
            fec == "on", breakout == "on",
            int(mtu) if mtu.strip() else None,
        )
    )
    return RedirectResponse(f"/switches/{hostname}#interfaces", status_code=303)


@app.post("/switches/{hostname}/interfaces/{iface_id}/delete")
async def interface_delete(hostname: str, iface_id: str):
    execute("DELETE FROM interfaces WHERE id = %s", (iface_id,))
    return RedirectResponse(f"/switches/{hostname}#interfaces", status_code=303)


# ---- BGP ----

@app.post("/switches/{hostname}/bgp/save", response_class=HTMLResponse)
async def bgp_save(
    request: Request, hostname: str,
    local_as: str = Form(...),
    router_id: str = Form(""),
    ebgp_requires_policy: str = Form("off"),
    max_paths: str = Form("4"),
    redistribute_connected: str = Form("on"),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO bgp_config (switch_id, local_as, router_id, ebgp_requires_policy, max_paths, redistribute_connected)
           VALUES (%s, %s, %s, %s, %s, %s)
           ON CONFLICT (switch_id) DO UPDATE SET
            local_as=EXCLUDED.local_as, router_id=EXCLUDED.router_id,
            ebgp_requires_policy=EXCLUDED.ebgp_requires_policy,
            max_paths=EXCLUDED.max_paths,
            redistribute_connected=EXCLUDED.redistribute_connected""",
        (switch["id"], int(local_as), router_id or None,
         ebgp_requires_policy == "on",
         int(max_paths) if max_paths.strip() else 4,
         redistribute_connected == "on")
    )
    return RedirectResponse(f"/switches/{hostname}#bgp", status_code=303)


@app.post("/switches/{hostname}/bgp/neighbor/add")
async def bgp_neighbor_add(
    hostname: str,
    neighbor_ip: str = Form(...),
    remote_as: str = Form(...),
    description: str = Form(""),
    bfd_enabled: str = Form("off"),
    ipv4_unicast: str = Form("on"),
):
    bgp = query_one(
        "SELECT b.id FROM bgp_config b JOIN switches s ON s.id=b.switch_id WHERE s.hostname=%s",
        (hostname,)
    )
    if not bgp:
        raise HTTPException(400, "Set BGP config first")
    execute(
        """INSERT INTO bgp_neighbors (bgp_id, neighbor_ip, remote_as, description, bfd_enabled, ipv4_unicast)
           VALUES (%s,%s,%s,%s,%s,%s)
           ON CONFLICT (bgp_id, neighbor_ip) DO UPDATE SET
            remote_as=EXCLUDED.remote_as, description=EXCLUDED.description,
            bfd_enabled=EXCLUDED.bfd_enabled, ipv4_unicast=EXCLUDED.ipv4_unicast""",
        (bgp["id"], neighbor_ip.strip(), int(remote_as),
         description or None, bfd_enabled == "on", ipv4_unicast == "on")
    )
    return RedirectResponse(f"/switches/{hostname}#bgp", status_code=303)


@app.post("/switches/{hostname}/bgp/neighbor/{n_id}/delete")
async def bgp_neighbor_delete(hostname: str, n_id: str):
    execute("DELETE FROM bgp_neighbors WHERE id = %s", (n_id,))
    return RedirectResponse(f"/switches/{hostname}#bgp", status_code=303)


@app.post("/switches/{hostname}/bgp/network/add")
async def bgp_network_add(hostname: str, network_prefix: str = Form(...)):
    bgp = query_one(
        "SELECT b.id FROM bgp_config b JOIN switches s ON s.id=b.switch_id WHERE s.hostname=%s",
        (hostname,)
    )
    if not bgp:
        raise HTTPException(400, "Set BGP config first")
    execute(
        "INSERT INTO bgp_networks (bgp_id, network_prefix) VALUES (%s,%s) ON CONFLICT DO NOTHING",
        (bgp["id"], network_prefix.strip())
    )
    return RedirectResponse(f"/switches/{hostname}#bgp", status_code=303)


@app.post("/switches/{hostname}/bgp/network/{net_id}/delete")
async def bgp_network_delete(hostname: str, net_id: str):
    execute("DELETE FROM bgp_networks WHERE id = %s", (net_id,))
    return RedirectResponse(f"/switches/{hostname}#bgp", status_code=303)


# ---- L3 Interfaces ----

@app.post("/switches/{hostname}/l3/add")
async def l3_add(
    hostname: str,
    interface_name: str = Form(...),
    ip_address: str = Form(""),
    prefix_length: str = Form(""),
    description: str = Form(""),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO l3_interfaces (switch_id, interface_name, ip_address, prefix_length, description)
           VALUES (%s,%s,%s,%s,%s)
           ON CONFLICT (switch_id, interface_name) DO UPDATE SET
            ip_address=EXCLUDED.ip_address, prefix_length=EXCLUDED.prefix_length,
            description=EXCLUDED.description""",
        (switch["id"], interface_name.strip(),
         ip_address or None,
         int(prefix_length) if prefix_length.strip() else None,
         description or None)
    )
    return RedirectResponse(f"/switches/{hostname}#l3", status_code=303)


@app.post("/switches/{hostname}/l3/{l3_id}/delete")
async def l3_delete(hostname: str, l3_id: str):
    execute("DELETE FROM l3_interfaces WHERE id = %s", (l3_id,))
    return RedirectResponse(f"/switches/{hostname}#l3", status_code=303)


# ---- VRRP ----

@app.post("/switches/{hostname}/vrrp/add")
async def vrrp_add(
    hostname: str,
    interface_name: str = Form(...),
    vrid: int = Form(...),
    virtual_ip: str = Form(...),
    load_balance: str = Form("off"),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO vrrp_config (switch_id, interface_name, vrid, virtual_ip, load_balance)
           VALUES (%s,%s,%s,%s,%s)
           ON CONFLICT (switch_id, interface_name, vrid) DO UPDATE SET
            virtual_ip=EXCLUDED.virtual_ip, load_balance=EXCLUDED.load_balance""",
        (switch["id"], interface_name.strip(), vrid, virtual_ip.strip(), load_balance == "on")
    )
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


@app.post("/switches/{hostname}/vrrp/{vrrp_id}/delete")
async def vrrp_delete(hostname: str, vrrp_id: str):
    execute("DELETE FROM vrrp_config WHERE id = %s", (vrrp_id,))
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


# ---- Static routes ----

@app.post("/switches/{hostname}/routes/add")
async def route_add(
    hostname: str,
    prefix: str = Form(...),
    next_hop: str = Form(...),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        "INSERT INTO static_routes (switch_id, prefix, next_hop) VALUES (%s,%s,%s) ON CONFLICT DO NOTHING",
        (switch["id"], prefix.strip(), next_hop.strip())
    )
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


@app.post("/switches/{hostname}/routes/{route_id}/delete")
async def route_delete(hostname: str, route_id: str):
    execute("DELETE FROM static_routes WHERE id = %s", (route_id,))
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


# ---- DHCP relay ----

@app.post("/switches/{hostname}/dhcp/add")
async def dhcp_add(
    hostname: str,
    interface_name: str = Form(...),
    dhcp_server_address: str = Form(...),
    relay_agent_address: str = Form(""),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO dhcp_relay (switch_id, interface_name, dhcp_server_address, relay_agent_address)
           VALUES (%s,%s,%s,%s)
           ON CONFLICT (switch_id, interface_name, dhcp_server_address) DO NOTHING""",
        (switch["id"], interface_name.strip(),
         dhcp_server_address.strip(), relay_agent_address or None)
    )
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


@app.post("/switches/{hostname}/dhcp/{dhcp_id}/delete")
async def dhcp_delete(hostname: str, dhcp_id: str):
    execute("DELETE FROM dhcp_relay WHERE id = %s", (dhcp_id,))
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


# ---- STP ----

@app.post("/switches/{hostname}/stp/save")
async def stp_save(
    hostname: str,
    protocol: str = Form("mstp"),
    bridge_priority: int = Form(32768),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO spanning_tree (switch_id, protocol, bridge_priority)
           VALUES (%s,%s,%s)
           ON CONFLICT (switch_id) DO UPDATE SET protocol=EXCLUDED.protocol, bridge_priority=EXCLUDED.bridge_priority""",
        (switch["id"], protocol, bridge_priority)
    )
    return RedirectResponse(f"/switches/{hostname}#routing", status_code=303)


# ---- CoS ----

@app.post("/switches/{hostname}/cos/add")
async def cos_add(
    hostname: str,
    class_name: str = Form(...),
    local_priority: str = Form(""),
):
    switch = query_one("SELECT id FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    execute(
        """INSERT INTO cos_forwarding_classes (switch_id, class_name, local_priority)
           VALUES (%s,%s,%s)
           ON CONFLICT (switch_id, class_name) DO UPDATE SET local_priority=EXCLUDED.local_priority""",
        (switch["id"], class_name.strip(),
         int(local_priority) if local_priority.strip() else None)
    )
    return RedirectResponse(f"/switches/{hostname}#qos", status_code=303)


@app.post("/switches/{hostname}/cos/{cos_id}/delete")
async def cos_delete(hostname: str, cos_id: str):
    execute("DELETE FROM cos_forwarding_classes WHERE id = %s", (cos_id,))
    return RedirectResponse(f"/switches/{hostname}#qos", status_code=303)


# ---------------------------------------------------------------------------
# Guide sync → interfaces
# ---------------------------------------------------------------------------

@app.post("/switches/{hostname}/sync-guide", response_class=HTMLResponse)
async def sync_guide(request: Request, hostname: str):
    switch = query_one("SELECT * FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    result = sync_guide_to_interfaces(switch["id"])
    msg = f"Sync complete — {result['created']} created, {result['updated']} updated"
    # Re-render detail page with flash message
    return RedirectResponse(f"/switches/{hostname}?msg={msg}", status_code=303)


# ---------------------------------------------------------------------------
# Export / Preview
# ---------------------------------------------------------------------------

@app.get("/switches/{hostname}/export")
async def export_config(hostname: str):
    """Download clean .txt config ready to load into the switch."""
    switch = query_one("SELECT hostname FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    config = build_config(hostname)
    filename = f"{hostname}.txt"
    return Response(
        content=config,
        media_type="text/plain",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )


@app.get("/switches/{hostname}/preview", response_class=HTMLResponse)
async def preview_config(request: Request, hostname: str):
    switch = query_one("SELECT * FROM switches WHERE hostname = %s", (hostname,))
    if not switch:
        raise HTTPException(404)
    config_text = build_config(hostname)
    line_count = len([l for l in config_text.splitlines() if l.strip()])
    return tmpl("preview.html", request,
                switch=switch, config_text=config_text, line_count=line_count)


# ---------------------------------------------------------------------------
# IPAM page
# ---------------------------------------------------------------------------

@app.get("/ipam", response_class=HTMLResponse)
async def ipam_page(request: Request):
    devices = query("""
        SELECT
            sg.id,
            sg.switch_name,
            sg.device_type,
            sg.building,
            sg.region,
            sg.status,
            cast(sg.ip as text)           AS ip,
            cast(sg.management_ip as text) AS management_ip,
            cast(sg.mgmt_l3_ip as text)   AS mgmt_l3_ip,
            cast(sg.loopback_ip as text)  AS loopback_ip,
            sg.loopback_subnet::text      AS loopback_subnet,
            sg.asn,
            sg.mlag_domain,
            sg.ae_range,
            sg.notes,
            sw.hostname                   AS linked_hostname
        FROM switch_guide sg
        LEFT JOIN switches sw
            ON UPPER(sw.hostname) = UPPER(sg.switch_name)
        ORDER BY sg.building, sg.device_type, sg.switch_name
    """)
    buildings    = sorted({d["building"] for d in devices if d["building"]})
    device_types = sorted({d["device_type"] for d in devices if d["device_type"]})
    regions      = sorted({d["region"] for d in devices if d["region"]})
    return tmpl("ipam.html", request,
                devices=devices, buildings=buildings,
                device_types=device_types, regions=regions)


# Guide pages
# ---------------------------------------------------------------------------

@app.get("/guide", response_class=HTMLResponse)
async def guide_page(request: Request, site: str = "", search: str = ""):
    where, params = [], []
    if site:
        where.append("sg.site = %s"); params.append(site)
    if search:
        where.append("(sg.switch_name ILIKE %s OR sg.model ILIKE %s OR cast(sg.management_ip as text) ILIKE %s)")
        params += [f"%{search}%"] * 3
    where_sql = ("WHERE " + " AND ".join(where)) if where else ""
    guide = query(f"""
        SELECT sg.*,
            COUNT(sc.id) AS connection_count,
            COUNT(sc.id) FILTER (WHERE sc.enabled) AS enabled_count
        FROM switch_guide sg
        LEFT JOIN switch_connections sc ON sc.guide_id = sg.id
        {where_sql}
        GROUP BY sg.id ORDER BY sg.site, sg.switch_name
    """, params or None)
    sites = query("SELECT DISTINCT site FROM switch_guide WHERE site IS NOT NULL ORDER BY site")
    return tmpl("guide.html", request, guide=guide,
                sites=[r["site"] for r in sites],
                current_site=site, search=search)


@app.get("/guide/{guide_id}", response_class=HTMLResponse)
async def guide_detail(request: Request, guide_id: str):
    gswitch = query_one("SELECT * FROM switch_guide WHERE id = %s", (guide_id,))
    if not gswitch:
        raise HTTPException(404)
    connections = query(
        "SELECT * FROM switch_connections WHERE guide_id = %s ORDER BY local_port",
        (guide_id,)
    )
    # Find matching switch in switches table
    linked_switch = None
    if gswitch.get("switch_name"):
        linked_switch = query_one(
            "SELECT hostname FROM switches WHERE hostname = %s",
            (gswitch["switch_name"],)
        )
    return tmpl("guide_detail.html", request,
                switch=gswitch, connections=connections, linked_switch=linked_switch)


@app.post("/guide/toggle/{conn_id}", response_class=HTMLResponse)
async def toggle_connection(request: Request, conn_id: str):
    conn = query_one("SELECT * FROM switch_connections WHERE id = %s", (conn_id,))
    if not conn:
        raise HTTPException(404)
    execute("UPDATE switch_connections SET enabled = NOT enabled WHERE id = %s", (conn_id,))
    conn["enabled"] = not conn["enabled"]
    return tmpl("_connection_row.html", request, conn=conn)


# ---------------------------------------------------------------------------
# Import
# ---------------------------------------------------------------------------

@app.get("/import", response_class=HTMLResponse)
async def import_page(request: Request):
    return tmpl("import.html", request, messages=[])


@app.post("/import/configs", response_class=HTMLResponse)
async def import_configs(request: Request, files: list[UploadFile] = File(...)):
    messages = []
    loader = DBLoader(dsn=DSN)
    try:
        loader.connect()
        for upload in files:
            if not upload.filename.endswith(".txt"):
                messages.append({"type": "warning", "text": f"Skipped (not .txt): {upload.filename}"})
                continue
            content = await upload.read()
            with tempfile.NamedTemporaryFile(suffix=".txt", delete=False) as tmp:
                tmp.write(content); tmp_path = tmp.name
            try:
                parser = PicOSParser(tmp_path)
                cfg = parser.parse()
                cfg.source_file = upload.filename
                loader.load_switch_configs([cfg])
                messages.append({"type": "success",
                    "text": f"Imported {upload.filename} → {cfg.hostname} ({len(cfg.vlans)} VLANs, {len(cfg.interfaces)} interfaces)"})
            except Exception as e:
                messages.append({"type": "error", "text": f"Failed {upload.filename}: {e}"})
            finally:
                os.unlink(tmp_path)
    finally:
        loader.close()
    return tmpl("import.html", request, messages=messages)


@app.post("/import/guide", response_class=HTMLResponse)
async def import_guide(request: Request, file: UploadFile = File(...)):
    messages = []
    content = await file.read()
    with tempfile.NamedTemporaryFile(suffix=".xlsx", delete=False) as tmp:
        tmp.write(content); tmp_path = tmp.name
    try:
        importer = ExcelImporter(tmp_path)
        switches, connections = importer.load()
        loader = DBLoader(dsn=DSN)
        loader.connect()
        try:
            loader.load_guide(switches, connections)
            messages.append({"type": "success",
                "text": f"Imported {len(switches)} switches, {len(connections)} connections from {file.filename}"})
        finally:
            loader.close()
    except Exception as e:
        messages.append({"type": "error", "text": f"Failed: {e}"})
    finally:
        os.unlink(tmp_path)
    return tmpl("import.html", request, messages=messages)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------

# ---------------------------------------------------------------------------
# Connectivity — Ping & SSH
# ---------------------------------------------------------------------------

@app.post("/switches/{hostname}/ping", response_class=HTMLResponse)
async def switch_ping(request: Request, hostname: str):
    """Ping the switch management IP. Returns an HTMX status badge."""
    from ssh_utils import ping_host
    switch = query_one(
        "SELECT id, management_ip, last_ping_ok, last_ping_ms FROM switches WHERE hostname = %s",
        (hostname,)
    )
    if not switch:
        raise HTTPException(404)

    ip = str(switch["management_ip"]) if switch["management_ip"] else None
    result = ping_host(ip)

    execute(
        "UPDATE switches SET last_ping_at=NOW(), last_ping_ok=%s, last_ping_ms=%s WHERE hostname=%s",
        (result.reachable, result.latency_ms, hostname)
    )

    return tmpl("_ping_status.html", request,
                hostname=hostname, result=result, ip=ip)


@app.post("/switches/{hostname}/ssh/test", response_class=HTMLResponse)
async def ssh_test(
    request: Request,
    hostname: str,
    password: str = Form(""),
):
    """Test SSH connectivity without downloading config."""
    from ssh_utils import ping_host, SshResult
    import paramiko, socket

    switch = query_one(
        "SELECT id, management_ip, ssh_username, ssh_port, ssh_password FROM switches WHERE hostname=%s",
        (hostname,)
    )
    if not switch:
        raise HTTPException(404)

    ip = str(switch["management_ip"]) if switch["management_ip"] else None
    user = switch["ssh_username"] or "root"
    port = switch["ssh_port"] or 22
    pwd  = password or switch.get("ssh_password") or ""

    # Save password if provided
    if password:
        execute("UPDATE switches SET ssh_password=%s WHERE hostname=%s", (password, hostname))

    result = SshResult(success=False)
    try:
        client = paramiko.SSHClient()
        client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
        client.connect(
            hostname=ip, port=port, username=user, password=pwd,
            timeout=10, look_for_keys=False, allow_agent=False
        )
        client.close()
        result = SshResult(success=True)
        execute("UPDATE switches SET last_ssh_at=NOW(), last_ssh_ok=true WHERE hostname=%s", (hostname,))
    except paramiko.AuthenticationException:
        result = SshResult(success=False, error="Authentication failed — check username/password")
        execute("UPDATE switches SET last_ssh_at=NOW(), last_ssh_ok=false WHERE hostname=%s", (hostname,))
    except (socket.timeout, TimeoutError):
        result = SshResult(success=False, error=f"Connection timed out to {ip}:{port}")
    except ConnectionRefusedError:
        result = SshResult(success=False, error=f"Connection refused at {ip}:{port}")
    except Exception as e:
        result = SshResult(success=False, error=str(e))

    return tmpl("_ssh_status.html", request,
                hostname=hostname, result=result, ip=ip, user=user)


@app.post("/switches/{hostname}/ssh/download", response_class=HTMLResponse)
async def ssh_download(
    request: Request,
    hostname: str,
    password: str = Form(""),
):
    """SSH to the switch, download running config, store it in the DB."""
    from ssh_utils import ssh_download_config, clean_config_output, diff_configs

    switch = query_one(
        "SELECT id, management_ip, ssh_username, ssh_port, ssh_password FROM switches WHERE hostname=%s",
        (hostname,)
    )
    if not switch:
        raise HTTPException(404)

    ip   = str(switch["management_ip"]) if switch["management_ip"] else None
    user = switch["ssh_username"] or "root"
    port = switch["ssh_port"] or 22
    pwd  = password or switch.get("ssh_password") or ""

    if password:
        execute("UPDATE switches SET ssh_password=%s WHERE hostname=%s", (password, hostname))

    result = ssh_download_config(ip=ip, username=user, password=pwd, port=port, timeout=30)

    if result.success and result.config_text:
        config = result.config_text
        line_count = len([l for l in config.splitlines() if l.strip()])

        # Diff vs previous download
        prev = query_one(
            "SELECT config_text FROM running_configs WHERE switch_id=%s ORDER BY downloaded_at DESC LIMIT 1",
            (switch["id"],)
        )
        diff_text = diff_configs(prev["config_text"], config) if prev else None

        execute(
            """INSERT INTO running_configs (switch_id, source_ip, config_text, line_count, diff_from_prev)
               VALUES (%s, %s, %s, %s, %s)""",
            (switch["id"], ip, config, line_count, diff_text)
        )
        execute(
            "UPDATE switches SET last_ssh_at=NOW(), last_ssh_ok=true WHERE hostname=%s",
            (hostname,)
        )

    return tmpl("_ssh_download_result.html", request,
                hostname=hostname, result=result, ip=ip)


@app.get("/switches/{hostname}/running-configs", response_class=HTMLResponse)
async def running_configs_page(request: Request, hostname: str):
    """Show history of downloaded running configs."""
    switch = query_one("SELECT * FROM switches WHERE hostname=%s", (hostname,))
    if not switch:
        raise HTTPException(404)
    configs = query(
        """SELECT id, downloaded_at, source_ip, line_count,
                  LEFT(config_text, 200) AS config_preview,
                  diff_from_prev IS NOT NULL AS has_diff
           FROM running_configs WHERE switch_id=%s
           ORDER BY downloaded_at DESC LIMIT 20""",
        (switch["id"],)
    )
    return tmpl("running_configs.html", request, switch=switch, configs=configs)


@app.get("/switches/{hostname}/running-configs/compare", response_class=HTMLResponse)
async def running_config_compare(request: Request, hostname: str, left: str = "", right: str = ""):
    """Side-by-side compare of two running configs."""
    switch = query_one("SELECT * FROM switches WHERE hostname=%s", (hostname,))
    if not switch:
        raise HTTPException(404)
    if not left or not right:
        raise HTTPException(400, "Select two configs to compare")
    cfg_left = query_one("SELECT * FROM running_configs WHERE id=%s AND switch_id=%s",
                         (left, switch["id"]))
    cfg_right = query_one("SELECT * FROM running_configs WHERE id=%s AND switch_id=%s",
                          (right, switch["id"]))
    if not cfg_left or not cfg_right:
        raise HTTPException(404)
    # Build line-by-line diff info
    import difflib
    left_lines = (cfg_left["config_text"] or "").splitlines()
    right_lines = (cfg_right["config_text"] or "").splitlines()
    sm = difflib.SequenceMatcher(None, left_lines, right_lines)
    diff_left = []
    diff_right = []
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            for l in left_lines[i1:i2]:
                diff_left.append({"text": l, "changed": False})
            for l in right_lines[j1:j2]:
                diff_right.append({"text": l, "changed": False})
        elif tag == "replace":
            left_chunk = left_lines[i1:i2]
            right_chunk = right_lines[j1:j2]
            max_len = max(len(left_chunk), len(right_chunk))
            for idx in range(max_len):
                if idx < len(left_chunk):
                    diff_left.append({"text": left_chunk[idx], "changed": True})
                else:
                    diff_left.append({"text": "", "changed": True, "blank": True})
                if idx < len(right_chunk):
                    diff_right.append({"text": right_chunk[idx], "changed": True})
                else:
                    diff_right.append({"text": "", "changed": True, "blank": True})
        elif tag == "delete":
            for l in left_lines[i1:i2]:
                diff_left.append({"text": l, "changed": True})
                diff_right.append({"text": "", "changed": True, "blank": True})
        elif tag == "insert":
            for l in right_lines[j1:j2]:
                diff_left.append({"text": "", "changed": True, "blank": True})
                diff_right.append({"text": l, "changed": True})
    return tmpl("running_config_compare.html", request, switch=switch,
                cfg_left=cfg_left, cfg_right=cfg_right,
                diff_left=diff_left, diff_right=diff_right)


@app.get("/switches/{hostname}/running-configs/{config_id}", response_class=HTMLResponse)
async def running_config_detail(request: Request, hostname: str, config_id: str):
    """View a specific downloaded config (full text + diff)."""
    switch = query_one("SELECT * FROM switches WHERE hostname=%s", (hostname,))
    if not switch:
        raise HTTPException(404)
    cfg = query_one("SELECT * FROM running_configs WHERE id=%s AND switch_id=%s",
                    (config_id, switch["id"]))
    if not cfg:
        raise HTTPException(404)
    return tmpl("running_config_detail.html", request, switch=switch, cfg=cfg)


@app.get("/switches/{hostname}/running-configs/{config_id}/download")
async def download_running_config(hostname: str, config_id: str):
    """Download a specific running config as .txt."""
    switch = query_one("SELECT id FROM switches WHERE hostname=%s", (hostname,))
    if not switch:
        raise HTTPException(404)
    cfg = query_one("SELECT * FROM running_configs WHERE id=%s AND switch_id=%s",
                    (config_id, switch["id"]))
    if not cfg:
        raise HTTPException(404)
    ts = cfg["downloaded_at"].strftime("%Y%m%d_%H%M%S") if cfg["downloaded_at"] else "unknown"
    filename = f"{hostname}_running_{ts}.txt"
    return Response(
        content=cfg["config_text"],
        media_type="text/plain",
        headers={"Content-Disposition": f'attachment; filename="{filename}"'},
    )


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("app:app", host="0.0.0.0", port=8000, reload=True,
                reload_dirs=[BASE_DIR], app_dir=BASE_DIR)
