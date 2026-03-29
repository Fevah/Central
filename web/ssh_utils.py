"""
SSH & Ping Utilities for Central
========================================
Handles ping reachability checks and SSH connections to PicOS 4.x switches.

PicOS SSH notes:
  - Switches have SSH root login enabled (set system services ssh root-login allow)
  - Management VRF is enabled — connect via management IP (VLAN-82 or eth0)
  - Running config command: "show configuration"
  - Output is in set-format, same as the .txt files
  - PicOS uses an interactive shell; we use invoke_shell for reliability
"""

from __future__ import annotations

import re
import subprocess
import platform
import socket
import time
from dataclasses import dataclass, field
from typing import Optional


# ---------------------------------------------------------------------------
# Result types
# ---------------------------------------------------------------------------

@dataclass
class PingResult:
    reachable: bool
    latency_ms: Optional[float] = None
    error: Optional[str] = None


@dataclass
class SshResult:
    success: bool
    config_text: Optional[str] = None
    raw_output: Optional[str] = None
    error: Optional[str] = None
    duration_ms: Optional[float] = None


# ---------------------------------------------------------------------------
# Ping
# ---------------------------------------------------------------------------

def ping_host(ip: str, count: int = 2, timeout_s: int = 2) -> PingResult:
    """
    Ping an IP address and return reachability + average latency.
    Works on Windows and Linux/Mac.
    """
    if not ip or not ip.strip():
        return PingResult(reachable=False, error="No IP configured")

    ip = ip.strip()

    # Validate IP looks sane before shelling out
    if not _valid_ip(ip):
        return PingResult(reachable=False, error=f"Invalid IP: {ip}")

    is_windows = platform.system().lower() == "windows"
    if is_windows:
        cmd = ["ping", "-n", str(count), "-w", str(timeout_s * 1000), ip]
    else:
        cmd = ["ping", "-c", str(count), "-W", str(timeout_s), ip]

    try:
        result = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout_s * count + 5,
        )
        output = result.stdout + result.stderr

        if result.returncode == 0:
            latency = _parse_latency(output, is_windows)
            return PingResult(reachable=True, latency_ms=latency)
        else:
            return PingResult(reachable=False, error="Host unreachable")

    except subprocess.TimeoutExpired:
        return PingResult(reachable=False, error="Ping timed out")
    except FileNotFoundError:
        return PingResult(reachable=False, error="ping command not found")
    except Exception as e:
        return PingResult(reachable=False, error=str(e))


def _valid_ip(ip: str) -> bool:
    """Basic IP address validation."""
    try:
        socket.inet_aton(ip)
        return True
    except socket.error:
        return False


def _parse_latency(output: str, is_windows: bool) -> Optional[float]:
    """Extract average round-trip time from ping output."""
    if is_windows:
        # Windows: "Average = 2ms" or "Mittelwert = 2ms"
        m = re.search(r'(?:Average|Mittelwert|avg)\s*=\s*(\d+(?:\.\d+)?)ms', output, re.IGNORECASE)
        if m:
            return float(m.group(1))
        # Try individual reply: "Reply from X: bytes=32 time=2ms"
        m = re.search(r'time[<=](\d+(?:\.\d+)?)ms', output, re.IGNORECASE)
        if m:
            return float(m.group(1))
    else:
        # Linux/Mac: "rtt min/avg/max/mdev = 0.4/0.5/0.6/0.05 ms"
        m = re.search(r'min/avg/max[^=]*=\s*[\d.]+/([\d.]+)/', output)
        if m:
            return float(m.group(1))
    return None


# ---------------------------------------------------------------------------
# SSH — download running config from PicOS
# ---------------------------------------------------------------------------

# PicOS requires entering config mode ("conf") then "sh | display set"
# Fallback commands tried if primary doesn't return set lines
PICOS_CONFIG_COMMANDS = [
    "sh | display set",
    "show | display set",
    "show configuration",
]

# Sent before config commands to enter configuration mode
PICOS_PRE_COMMANDS = ["conf"]

# Lines to strip from PicOS SSH output (prompts, banners, etc.)
_STRIP_PATTERNS = [
    re.compile(r'^[A-Za-z0-9_\-\.]+[@#>]\s*.*$'),   # shell/CLI prompts
    re.compile(r'^Last login:'),
    re.compile(r'^Welcome to'),
    re.compile(r'^\s*$'),                              # blank lines
    re.compile(r'^Building configuration'),
    re.compile(r'^Current configuration'),
    re.compile(r'^\-+$'),                              # divider lines
]


def ssh_download_config(
    ip: str,
    username: str = "root",
    password: Optional[str] = None,
    port: int = 22,
    timeout: int = 30,
    key_filename: Optional[str] = None,
) -> SshResult:
    """
    SSH to a PicOS switch and download the running configuration.

    Returns SshResult with config_text containing clean set-format commands.

    Tries exec_command first (faster), falls back to invoke_shell
    if the device requires an interactive session.
    """
    try:
        import paramiko
    except ImportError:
        return SshResult(success=False, error="paramiko not installed: pip install paramiko")

    if not ip or not ip.strip():
        return SshResult(success=False, error="No IP address configured for this switch")

    ip = ip.strip()
    t0 = time.time()

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())

    connect_kwargs: dict = {
        "hostname":  ip,
        "port":      port,
        "username":  username,
        "timeout":   timeout,
        "look_for_keys": False,
        "allow_agent":   False,
    }
    if key_filename:
        connect_kwargs["key_filename"] = key_filename
    elif password:
        connect_kwargs["password"] = password

    try:
        client.connect(**connect_kwargs)
    except paramiko.AuthenticationException:
        return SshResult(success=False, error=f"Authentication failed for {username}@{ip}")
    except paramiko.SSHException as e:
        return SshResult(success=False, error=f"SSH error: {e}")
    except (socket.timeout, TimeoutError):
        return SshResult(success=False, error=f"Connection timed out to {ip}:{port}")
    except ConnectionRefusedError:
        return SshResult(success=False, error=f"Connection refused at {ip}:{port}")
    except OSError as e:
        return SshResult(success=False, error=f"Network error: {e}")

    try:
        # Try exec_command first — clean and fast
        config, raw = _try_exec_command(client, timeout)
        if config:
            ms = (time.time() - t0) * 1000
            return SshResult(success=True, config_text=config, raw_output=raw, duration_ms=ms)

        # Fall back to interactive shell (needed on some PicOS versions)
        config, raw = _try_interactive_shell(client, timeout)
        ms = (time.time() - t0) * 1000
        if config:
            return SshResult(success=True, config_text=config, raw_output=raw, duration_ms=ms)
        else:
            return SshResult(
                success=False,
                raw_output=raw,
                error="Connected but could not retrieve configuration. Check SSH credentials and PicOS CLI access.",
                duration_ms=ms,
            )
    finally:
        client.close()


def _try_exec_command(client, timeout: int) -> tuple[Optional[str], str]:
    """Try each config command via exec_command. Returns (clean_config, raw)."""
    for cmd in PICOS_CONFIG_COMMANDS:
        try:
            stdin, stdout, stderr = client.exec_command(cmd, timeout=timeout)
            raw = stdout.read().decode("utf-8", errors="replace")
            err = stderr.read().decode("utf-8", errors="replace")

            lines = [l for l in raw.splitlines() if l.strip().startswith("set ")]
            if len(lines) > 5:
                return "\n".join(lines), raw
        except Exception:
            continue
    return None, ""


def _try_interactive_shell(client, timeout: int) -> tuple[Optional[str], str]:
    """
    Use an interactive shell channel to run config commands.
    Handles PicOS interactive CLI, waits for prompt, sends command.
    """
    shell = client.invoke_shell(width=220, height=50)
    shell.settimeout(timeout)

    raw_parts = []

    def _read_until_quiet(wait=2.0, chunk_size=4096) -> str:
        """Read until no data for `wait` seconds."""
        buf = ""
        shell.settimeout(wait)
        try:
            while True:
                chunk = shell.recv(chunk_size).decode("utf-8", errors="replace")
                if not chunk:
                    break
                buf += chunk
        except Exception:
            pass
        return buf

    # Drain the welcome banner / initial prompt
    banner = _read_until_quiet(wait=3.0)
    raw_parts.append(banner)

    # Disable paging + enter config mode
    for pre in ["terminal length 0", "set cli screen-length 0"] + PICOS_PRE_COMMANDS:
        shell.send(pre + "\n")
        time.sleep(0.5)
        raw_parts.append(_read_until_quiet(wait=2.0))

    best_config = None

    for cmd in PICOS_CONFIG_COMMANDS:
        shell.send(cmd + "\n")
        time.sleep(0.5)
        output = _read_until_quiet(wait=3.0)
        raw_parts.append(output)

        lines = [l.strip() for l in output.splitlines() if l.strip().startswith("set ")]
        if len(lines) > 5:
            best_config = "\n".join(lines)
            break

    shell.send("exit\n")

    full_raw = "\n".join(raw_parts)
    return best_config, full_raw


# ---------------------------------------------------------------------------
# Clean and parse downloaded config
# ---------------------------------------------------------------------------

def clean_config_output(raw: str) -> str:
    """
    Strip terminal control codes, prompts and noise from SSH output.
    Returns only the set-command lines.
    """
    # Strip ANSI escape codes
    ansi = re.compile(r'\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])')
    text = ansi.sub("", raw)

    lines = []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("set "):
            lines.append(stripped)

    return "\n".join(lines)


def diff_configs(old: str, new: str) -> str:
    """Return a unified diff between two configs."""
    import difflib
    old_lines = old.splitlines(keepends=True)
    new_lines = new.splitlines(keepends=True)
    diff = difflib.unified_diff(
        old_lines, new_lines,
        fromfile="previous",
        tofile="downloaded",
        lineterm="",
    )
    return "".join(diff)
