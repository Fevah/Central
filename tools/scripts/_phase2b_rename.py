#!/usr/bin/env python3
"""Phase 2b: rename 3 projects (assembly, namespace, folder).

    Central.Core        -> Central.Engine       (libs/core        -> libs/engine)
    Central.Data        -> Central.Persistence  (libs/data        -> libs/persistence)
    Central.Api.Client  -> Central.ApiClient    (libs/api-client  stays)

Run from repo root. One-shot script. After run + verified green build,
the script can be deleted (lives in tools/scripts/ alongside _phase2a).
"""
import os
import re
import shutil
import subprocess
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent.parent

RENAMES = [
    # (old_assembly, new_assembly, old_folder_rel, new_folder_rel)
    ("Central.Core",       "Central.Engine",       "libs/core",       "libs/engine"),
    ("Central.Data",       "Central.Persistence",  "libs/data",       "libs/persistence"),
    ("Central.Api.Client", "Central.ApiClient",    "libs/api-client", "libs/api-client"),
]

# File extensions whose contents need namespace rewrites
TEXT_EXTS = {".cs", ".csproj", ".xaml", ".sln", ".json", ".config", ".targets",
             ".props", ".md", ".yml", ".yaml"}

# Skip these directories entirely (never rewrite their contents)
SKIP_DIRS = {"bin", "obj", "node_modules", ".git", ".vs", ".vscode",
             "packages-offline", "backups"}


def run(cmd, **kw):
    print(f"$ {' '.join(cmd) if isinstance(cmd, list) else cmd}")
    return subprocess.run(cmd, check=True, cwd=REPO, **kw)


def rename_folders_and_csprojs():
    for old_asm, new_asm, old_folder, new_folder in RENAMES:
        # Folder rename (if actually changing)
        if old_folder != new_folder:
            src = REPO / old_folder
            dst = REPO / new_folder
            dst.parent.mkdir(parents=True, exist_ok=True)
            proc = subprocess.run(["git", "mv", old_folder, new_folder],
                                  cwd=REPO, capture_output=True, text=True)
            if proc.returncode == 0:
                print(f"  git mv {old_folder} -> {new_folder}")
            else:
                shutil.move(str(src), str(dst))
                print(f"  fs mv  {old_folder} -> {new_folder}  (untracked in git)")

        # Csproj rename
        old_csproj = REPO / new_folder / f"{old_asm}.csproj"
        new_csproj = REPO / new_folder / f"{new_asm}.csproj"
        if old_csproj.exists() and old_csproj != new_csproj:
            proc = subprocess.run(
                ["git", "mv", str(old_csproj.relative_to(REPO)), str(new_csproj.relative_to(REPO))],
                cwd=REPO, capture_output=True, text=True,
            )
            if proc.returncode == 0:
                print(f"  git mv {old_asm}.csproj -> {new_asm}.csproj")
            else:
                shutil.move(str(old_csproj), str(new_csproj))
                print(f"  fs mv  {old_asm}.csproj -> {new_asm}.csproj  (untracked)")


def rewrite_all_content():
    """Scan every text file under the repo and apply the namespace/assembly renames."""
    # Build ordered list of (old, new) string replacements.
    # Order matters: do longest/most-specific first so partial matches don't clobber.
    replacements = []
    for old_asm, new_asm, _, _ in RENAMES:
        # For the dotted assembly "Central.Api.Client" we need careful handling:
        # it's longer than plain "Central.Api" so catching it first is fine.
        replacements.extend([
            (f"namespace {old_asm}", f"namespace {new_asm}"),
            (f"using {old_asm}",     f"using {new_asm}"),
            # Member access: "Central.Core.Foo" — use word-boundary via trailing chars
            (f"{old_asm}.",          f"{new_asm}."),
            (f"{old_asm};",          f"{new_asm};"),
            (f"{old_asm}>",          f"{new_asm}>"),
            (f"{old_asm})",          f"{new_asm})"),
            (f"{old_asm} ",          f"{new_asm} "),
            (f"{old_asm}\"",         f"{new_asm}\""),
            # csproj AssemblyName / RootNamespace xml values
            (f">{old_asm}<",         f">{new_asm}<"),
            # ProjectReference Include paths — just the csproj filename portion
            (f"{old_asm}.csproj",    f"{new_asm}.csproj"),
        ])
    # Sort longest-old-first to avoid a shorter match eating into a longer one.
    replacements.sort(key=lambda p: -len(p[0]))

    # Folder renames (inside relative paths in .sln and .csproj)
    folder_map = {}
    for _, _, old_folder, new_folder in RENAMES:
        if old_folder != new_folder:
            folder_map[old_folder.replace("/", "\\")] = new_folder.replace("/", "\\")
            folder_map[old_folder] = new_folder

    rewritten = 0
    for path in REPO.rglob("*"):
        if not path.is_file():
            continue
        if any(part in SKIP_DIRS for part in path.parts):
            continue
        if path.suffix.lower() not in TEXT_EXTS:
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except (UnicodeDecodeError, PermissionError):
            continue
        original = text
        for old, new in replacements:
            text = text.replace(old, new)
        for old, new in folder_map.items():
            text = text.replace(old, new)
        if text != original:
            path.write_text(text, encoding="utf-8-sig")
            rewritten += 1
    print(f"  rewrote {rewritten} files")


if __name__ == "__main__":
    print("=== Phase 2b: folder + csproj renames ===")
    rename_folders_and_csprojs()
    print("\n=== Rewriting namespaces / usings / refs across text files ===")
    rewrite_all_content()
    print("\nDone. Now run: dotnet build Central.sln --configuration Release -p:Platform=x64")
