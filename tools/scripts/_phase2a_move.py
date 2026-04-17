#!/usr/bin/env python3
"""Phase 2a of the repo restructure: move all .NET projects to the new tree.

One-shot script. After it runs and the build is verified green, delete it
(lives in tools/scripts/ purely so it's grouped with other dev utilities,
prefixed with _ so it doesn't get confused with a permanent tool).
"""
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent

# Source -> destination (folder-level). Project names are unchanged in this phase;
# the assembly renames happen in Phase 2b.
MOVES = {
    # Shared libraries
    "desktop/Central.Core":          "libs/core",
    "desktop/Central.Data":          "libs/data",
    "desktop/Central.Api.Client":    "libs/api-client",
    "desktop/Central.Workflows":     "libs/workflows",
    "desktop/Central.Security":      "libs/security",
    "desktop/Central.Tenancy":       "libs/tenancy",
    "desktop/Central.Licensing":     "libs/licensing",
    "desktop/Central.Observability": "libs/observability",
    "desktop/Central.Collaboration": "libs/collaboration",
    "desktop/Central.Protection":    "libs/protection",
    "desktop/Central.UpdateClient":  "libs/update-client",
    # WPF feature modules
    "desktop/Central.Module.Admin":       "modules/admin",
    "desktop/Central.Module.Audit":       "modules/audit",
    "desktop/Central.Module.CRM":         "modules/crm",
    "desktop/Central.Module.Dashboard":   "modules/dashboard",
    "desktop/Central.Module.Devices":     "modules/devices",
    "desktop/Central.Module.GlobalAdmin": "modules/global-admin",
    "desktop/Central.Module.Links":       "modules/links",
    "desktop/Central.Module.Routing":     "modules/routing",
    "desktop/Central.Module.ServiceDesk": "modules/service-desk",
    "desktop/Central.Module.Switches":    "modules/switches",
    "desktop/Central.Module.Tasks":       "modules/tasks",
    "desktop/Central.Module.VLANs":       "modules/vlans",
    # API + desktop + tests
    "desktop/Central.Api":     "services/api",
    "desktop/Central.Desktop": "apps/desktop",
    "desktop/Central.Tests":   "tests/dotnet",
}

# csproj filename lookup by old project folder name (last segment of old path)
CSPROJ_BY_OLD_FOLDER = {
    "Central.Core":          "Central.Core.csproj",
    "Central.Data":          "Central.Data.csproj",
    "Central.Api.Client":    "Central.Api.Client.csproj",
    "Central.Workflows":     "Central.Workflows.csproj",
    "Central.Security":      "Central.Security.csproj",
    "Central.Tenancy":       "Central.Tenancy.csproj",
    "Central.Licensing":     "Central.Licensing.csproj",
    "Central.Observability": "Central.Observability.csproj",
    "Central.Collaboration": "Central.Collaboration.csproj",
    "Central.Protection":    "Central.Protection.csproj",
    "Central.UpdateClient":  "Central.UpdateClient.csproj",
    "Central.Module.Admin":       "Central.Module.Admin.csproj",
    "Central.Module.Audit":       "Central.Module.Audit.csproj",
    "Central.Module.CRM":         "Central.Module.CRM.csproj",
    "Central.Module.Dashboard":   "Central.Module.Dashboard.csproj",
    "Central.Module.Devices":     "Central.Module.Devices.csproj",
    "Central.Module.GlobalAdmin": "Central.Module.GlobalAdmin.csproj",
    "Central.Module.Links":       "Central.Module.Links.csproj",
    "Central.Module.Routing":     "Central.Module.Routing.csproj",
    "Central.Module.ServiceDesk": "Central.Module.ServiceDesk.csproj",
    "Central.Module.Switches":    "Central.Module.Switches.csproj",
    "Central.Module.Tasks":       "Central.Module.Tasks.csproj",
    "Central.Module.VLANs":       "Central.Module.VLANs.csproj",
    "Central.Api":     "Central.Api.csproj",
    "Central.Desktop": "Central.Desktop.csproj",
    "Central.Tests":   "Central.Tests.csproj",
}


def run(cmd, **kw):
    print(f"$ {' '.join(cmd) if isinstance(cmd, list) else cmd}")
    return subprocess.run(cmd, check=True, cwd=REPO, **kw)


def git_mv_all():
    # Remove the .gitkeep files once at the start; recreate the dirs so git mv has a target.
    for parent in {"apps", "libs", "modules", "services", "tests", "tools", "assets"}:
        gk = REPO / parent / ".gitkeep"
        if gk.exists():
            subprocess.run(["git", "rm", "-f", f"{parent}/.gitkeep"], cwd=REPO, check=False)
        (REPO / parent).mkdir(parents=True, exist_ok=True)

    for src, dst in MOVES.items():
        src_path = REPO / src
        dst_path = REPO / dst
        dst_path.parent.mkdir(parents=True, exist_ok=True)
        if dst_path.exists():
            print(f"  already moved: {src} -> {dst}")
            continue
        # Prefer git mv (preserves history). Fall back to shutil.move for untracked folders.
        proc = subprocess.run(["git", "mv", src, dst], cwd=REPO, capture_output=True, text=True)
        if proc.returncode == 0:
            print(f"  git mv {src} -> {dst}")
        else:
            # Untracked folder — plain fs move. Git rename detection picks it up via content hash.
            shutil.move(str(src_path), str(dst_path))
            print(f"  fs mv  {src} -> {dst}  (untracked in git)")


def rewrite_project_references():
    """For every .csproj at its new location, rewrite ProjectReference paths."""
    ref_re = re.compile(r'(<ProjectReference\s+Include=")([^"]+)(")')

    # Build forward-lookup: old folder name -> new full path from repo root
    new_location_by_old_folder = {}
    for old, new in MOVES.items():
        folder_name = Path(old).name  # e.g. Central.Core
        new_location_by_old_folder[folder_name] = new  # e.g. libs/core

    for old_path, new_path in MOVES.items():
        csproj_name = CSPROJ_BY_OLD_FOLDER[Path(old_path).name]
        csproj_file = REPO / new_path / csproj_name
        if not csproj_file.exists():
            print(f"  SKIP (not found): {csproj_file}")
            continue

        text = csproj_file.read_text(encoding="utf-8")
        original = text

        def repl(m):
            prefix, old_ref, suffix = m.group(1), m.group(2), m.group(3)
            # old_ref looks like: ..\Central.Core\Central.Core.csproj
            # extract the folder name
            parts = re.split(r"[\\/]", old_ref)
            if len(parts) < 2 or parts[0] != "..":
                return m.group(0)  # untouched
            target_folder = parts[1]
            target_csproj = parts[-1]
            if target_folder not in new_location_by_old_folder:
                return m.group(0)
            target_new_path = REPO / new_location_by_old_folder[target_folder] / target_csproj
            # compute relative path from THIS csproj's parent to target_new_path
            rel = os.path.relpath(target_new_path, csproj_file.parent)
            rel = rel.replace("/", "\\")  # csproj convention
            return f"{prefix}{rel}{suffix}"

        new_text = ref_re.sub(repl, text)
        if new_text != original:
            csproj_file.write_text(new_text, encoding="utf-8")
            print(f"  rewrote refs in {csproj_file.relative_to(REPO)}")


def move_sln_to_root():
    old_sln = REPO / "desktop" / "Central.sln"
    new_sln = REPO / "Central.sln"
    if not old_sln.exists():
        print("Central.sln not found at desktop/ — skipping")
        return
    # Move with git so history is preserved
    subprocess.run(["git", "mv", "desktop/Central.sln", "Central.sln"], cwd=REPO, check=True)
    # Rewrite project paths in the .sln
    text = new_sln.read_text(encoding="utf-8-sig")  # .sln files often have BOM

    # Map: old ref "Central.X\Central.X.csproj" -> new ref "<new_dir>\Central.X.csproj"
    for old_path, new_path in MOVES.items():
        old_folder = Path(old_path).name
        csproj_name = CSPROJ_BY_OLD_FOLDER[old_folder]
        old_ref = f"{old_folder}\\{csproj_name}"
        new_ref = new_path.replace("/", "\\") + "\\" + csproj_name
        text = text.replace(old_ref, new_ref)

    new_sln.write_text(text, encoding="utf-8-sig")
    print(f"  rewrote {new_sln.relative_to(REPO)}")


def move_nuget_config():
    old = REPO / "desktop" / "NuGet.config"
    new = REPO / "NuGet.config"
    if old.exists() and not new.exists():
        subprocess.run(["git", "mv", "desktop/NuGet.config", "NuGet.config"], cwd=REPO, check=True)
        print("  moved NuGet.config to root")


def cleanup_empty_desktop():
    desktop = REPO / "desktop"
    if desktop.exists():
        remaining = list(desktop.iterdir())
        if not remaining:
            desktop.rmdir()
            print("  removed empty desktop/")
        else:
            print(f"  desktop/ still has {len(remaining)} items (will be handled in Phase 4):")
            for r in remaining:
                print(f"    - {r.relative_to(REPO)}")


if __name__ == "__main__":
    print("=== Phase 2a: folder moves ===")
    git_mv_all()
    print("\n=== Rewriting ProjectReference paths ===")
    rewrite_project_references()
    print("\n=== Moving Central.sln to root ===")
    move_sln_to_root()
    print("\n=== Moving NuGet.config to root ===")
    move_nuget_config()
    print("\n=== Cleaning up desktop/ ===")
    cleanup_empty_desktop()
    print("\nDone. Now run: dotnet build Central.sln --configuration Release -p:Platform=x64")
