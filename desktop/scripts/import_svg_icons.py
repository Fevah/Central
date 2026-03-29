"""
Import SVG icons into Central icon_library from popular open-source icon sets.
Categories: Networking, Business, UI, Hardware, Cloud, Security, DevOps

Max 20,000 icons total.

Usage: python import_svg_icons.py
"""
import os
import sys
import json
import urllib.request
import zipfile
import tempfile
import psycopg2

DSN = os.environ.get("CENTRAL_DSN",
    "host=localhost port=5432 dbname=central user=central password=central")

MAX_ICONS = 20000

# Icon sources — curated networking + business SVGs
ICON_SETS = [
    {
        "name": "Tabler Icons",
        "url": "https://github.com/tabler/tabler-icons/archive/refs/heads/main.zip",
        "svg_path": "tabler-icons-main/icons/outline",
        "category_prefix": "Tabler",
        "categories": {
            "network": ["network", "router", "server", "database", "cloud", "wifi", "antenna", "bluetooth",
                       "ethernet", "lan", "wan", "vpn", "firewall", "switch", "hub", "modem", "cable"],
            "business": ["building", "briefcase", "chart", "dashboard", "file", "folder", "mail",
                        "phone", "calendar", "clock", "user", "users", "settings", "tool",
                        "report", "invoice", "receipt", "wallet", "currency", "bank"],
            "hardware": ["cpu", "memory", "disk", "monitor", "keyboard", "mouse", "printer",
                        "camera", "usb", "plug", "battery", "power", "chip", "circuit"],
            "ui": ["arrow", "check", "x", "plus", "minus", "search", "filter", "sort", "edit",
                   "delete", "save", "copy", "paste", "undo", "redo", "refresh", "download",
                   "upload", "share", "link", "lock", "unlock", "eye", "bell", "star", "heart",
                   "home", "menu", "grid", "list", "table", "layout"],
            "security": ["shield", "key", "lock", "fingerprint", "scan", "alert", "warning",
                        "certificate", "encrypt", "decrypt", "auth", "permission"],
            "cloud": ["cloud", "server", "container", "docker", "kubernetes", "api", "webhook",
                     "microservice", "load-balancer", "cdn"],
        }
    }
]

def download_and_extract(url, dest_dir):
    """Download a zip file and extract to dest_dir."""
    print(f"  Downloading {url}...")
    zip_path = os.path.join(dest_dir, "icons.zip")
    urllib.request.urlretrieve(url, zip_path)
    print(f"  Extracting...")
    with zipfile.ZipFile(zip_path, 'r') as z:
        z.extractall(dest_dir)
    os.remove(zip_path)

def categorize_icon(filename, categories):
    """Determine category based on filename keywords."""
    name = filename.lower()
    for cat, keywords in categories.items():
        for kw in keywords:
            if kw in name:
                return cat
    return "general"

def import_icons():
    conn = psycopg2.connect(DSN)
    cur = conn.cursor()

    # Check current count
    cur.execute("SELECT count(*) FROM icon_library")
    current_count = cur.fetchone()[0]
    print(f"Current icons in DB: {current_count}")
    remaining = MAX_ICONS - current_count

    if remaining <= 0:
        print(f"Already at max ({MAX_ICONS}). Delete some icons first.")
        return

    total_imported = 0

    for icon_set in ICON_SETS:
        if total_imported >= remaining:
            break

        print(f"\nProcessing: {icon_set['name']}")

        with tempfile.TemporaryDirectory() as tmpdir:
            try:
                download_and_extract(icon_set["url"], tmpdir)
            except Exception as e:
                print(f"  Download failed: {e}")
                # Fall back to generating simple SVG icons
                print("  Generating built-in networking + business SVG icons instead...")
                total_imported += generate_builtin_icons(cur, conn, remaining - total_imported)
                continue

            svg_dir = os.path.join(tmpdir, icon_set["svg_path"])
            if not os.path.exists(svg_dir):
                # Try finding svg files recursively
                for root, dirs, files in os.walk(tmpdir):
                    svg_files = [f for f in files if f.endswith('.svg')]
                    if len(svg_files) > 10:
                        svg_dir = root
                        break

            if not os.path.exists(svg_dir):
                print(f"  SVG directory not found, skipping")
                continue

            svg_files = [f for f in os.listdir(svg_dir) if f.endswith('.svg')]
            print(f"  Found {len(svg_files)} SVG files")

            for svg_file in sorted(svg_files):
                if total_imported >= remaining:
                    break

                filepath = os.path.join(svg_dir, svg_file)
                name = os.path.splitext(svg_file)[0].replace('-', ' ').replace('_', ' ').title()
                category = categorize_icon(svg_file, icon_set.get("categories", {}))
                full_category = f"{icon_set['category_prefix']} {category.title()}"

                try:
                    with open(filepath, 'r', encoding='utf-8') as f:
                        svg_data = f.read()

                    cur.execute("""
                        INSERT INTO icon_library (name, category, size, svg_data, icon_format)
                        VALUES (%s, %s, 'svg', %s, 'svg')
                        ON CONFLICT DO NOTHING
                    """, (name, full_category, svg_data))
                    total_imported += 1
                except Exception as e:
                    pass  # skip problematic files

            conn.commit()
            print(f"  Imported {total_imported} icons so far")

    conn.commit()
    cur.close()
    conn.close()
    print(f"\nDone! Total imported: {total_imported}")

def generate_builtin_icons(cur, conn, max_count):
    """Generate built-in SVG icons for networking and business categories."""
    icons = {
        # Networking
        "Networking": [
            ("Router", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="14" width="20" height="6" rx="1"/><line x1="6" y1="14" x2="6" y2="20"/><line x1="18" y1="14" x2="18" y2="20"/><line x1="12" y1="4" x2="12" y2="14"/><circle cx="12" cy="4" r="2"/><line x1="8" y1="8" x2="12" y2="4"/><line x1="16" y1="8" x2="12" y2="4"/></svg>'),
            ("Switch", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="8" width="20" height="8" rx="1"/><circle cx="6" cy="12" r="1" fill="currentColor"/><circle cx="10" cy="12" r="1" fill="currentColor"/><circle cx="14" cy="12" r="1" fill="currentColor"/><circle cx="18" cy="12" r="1" fill="currentColor"/></svg>'),
            ("Firewall", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="18" height="18" rx="2"/><line x1="3" y1="9" x2="21" y2="9"/><line x1="3" y1="15" x2="21" y2="15"/><line x1="9" y1="3" x2="9" y2="21"/><line x1="15" y1="3" x2="15" y2="21"/></svg>'),
            ("Server", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="2" width="20" height="8" rx="2"/><rect x="2" y="14" width="20" height="8" rx="2"/><line x1="6" y1="6" x2="6.01" y2="6"/><line x1="6" y1="18" x2="6.01" y2="18"/></svg>'),
            ("Database", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/></svg>'),
            ("Network", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="5" r="3"/><circle cx="5" cy="19" r="3"/><circle cx="19" cy="19" r="3"/><line x1="12" y1="8" x2="5" y2="16"/><line x1="12" y1="8" x2="19" y2="16"/></svg>'),
            ("Cloud", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z"/></svg>'),
            ("WiFi", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M5 12.55a11 11 0 0 1 14.08 0"/><path d="M1.42 9a16 16 0 0 1 21.16 0"/><path d="M8.53 16.11a6 6 0 0 1 6.95 0"/><line x1="12" y1="20" x2="12.01" y2="20"/></svg>'),
            ("Ethernet Port", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="16" height="16" rx="2"/><line x1="8" y1="8" x2="8" y2="16"/><line x1="12" y1="8" x2="12" y2="16"/><line x1="16" y1="8" x2="16" y2="16"/></svg>'),
            ("VPN", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/><path d="M9 12l2 2 4-4"/></svg>'),
        ],
        # Business
        "Business": [
            ("Dashboard", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/></svg>'),
            ("Chart", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/></svg>'),
            ("Users", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>'),
            ("Settings", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>'),
            ("Folder", '<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z"/></svg>'),
        ],
    }

    imported = 0
    for category, icon_list in icons.items():
        for name, svg in icon_list:
            if imported >= max_count:
                break
            try:
                cur.execute("""
                    INSERT INTO icon_library (name, category, size, svg_data, icon_format)
                    VALUES (%s, %s, 'svg', %s, 'svg')
                    ON CONFLICT DO NOTHING
                """, (name, category, svg))
                imported += 1
            except:
                pass
    conn.commit()
    print(f"  Generated {imported} built-in SVG icons")
    return imported

if __name__ == "__main__":
    import_icons()
