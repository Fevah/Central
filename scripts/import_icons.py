#!/usr/bin/env python3
"""Bulk import icons from the icons/ folder into the icon_library DB table.

Usage:
    python scripts/import_icons.py [--dsn DSN] [--icons-dir PATH] [--sizes 16x16,32x32]

Default DSN: postgresql://switchbuilder:switchbuilder@localhost:5432/switchbuilder
Default icons dir: ./icons/
Default sizes: 16x16,32x32 (skip 24x24, 48x48, 64x64 to keep DB small)
"""

import os
import sys
import argparse
import psycopg2

def import_icons(dsn, icons_dir, sizes):
    conn = psycopg2.connect(dsn)
    cur = conn.cursor()

    total = 0
    skipped = 0

    for category in sorted(os.listdir(icons_dir)):
        cat_path = os.path.join(icons_dir, category)
        if not os.path.isdir(cat_path):
            continue

        for size in sorted(os.listdir(cat_path)):
            if size not in sizes:
                continue
            size_path = os.path.join(cat_path, size)
            if not os.path.isdir(size_path):
                continue

            for filename in sorted(os.listdir(size_path)):
                if not filename.lower().endswith('.png'):
                    continue

                filepath = os.path.join(size_path, filename)
                name = os.path.splitext(filename)[0]

                with open(filepath, 'rb') as f:
                    icon_data = f.read()

                try:
                    cur.execute("""
                        INSERT INTO icon_library (name, category, subcategory, size, icon_data, file_path)
                        VALUES (%s, %s, %s, %s, %s, %s)
                        ON CONFLICT (name, category, size) DO NOTHING
                    """, (name, category, '', size, psycopg2.Binary(icon_data), filepath))
                    total += 1
                except Exception as e:
                    skipped += 1

                if total % 500 == 0 and total > 0:
                    conn.commit()
                    print(f"  {total} icons imported...")

    conn.commit()
    cur.close()
    conn.close()
    print(f"\nDone: {total} icons imported, {skipped} skipped")

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Import icons into DB')
    parser.add_argument('--dsn', default='postgresql://switchbuilder:switchbuilder@localhost:5432/switchbuilder')
    parser.add_argument('--icons-dir', default='./icons/')
    parser.add_argument('--sizes', default='16x16,32x32', help='Comma-separated sizes to import')
    args = parser.parse_args()

    sizes = set(args.sizes.split(','))
    print(f"Importing from {args.icons_dir} (sizes: {sizes})")
    import_icons(args.dsn, args.icons_dir, sizes)
