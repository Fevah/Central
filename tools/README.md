# tools/

Dev-time utilities. **Not shipped.** Not on the critical path. CI doesn't build these.

If you're a new contributor, you can safely ignore everything in here until you need one of these specific tools.

## What's here

| Folder | What it is |
|--------|------------|
| [scripts/](scripts/) | Shell scripts: `backup.sh` (one-shot DB dump), `check-services.sh` (ping every platform endpoint for a quick health read), `migrate-users-to-auth-service.sh` (one-off migration from legacy auth to `auth-service`). |
| [parser/](parser/) | Python utilities for PicOS switch config + Excel guide: `picos_parser.py` (parse `set`-style config files), `db_loader.py` (load parsed configs into PG), `excel_importer.py` (read `switch_guide.xlsx`), `import_all_sheets.py` (batch import). |
| [icons/](icons/) | Python utilities for the icon library: `import_icons.py` (bulk import PNG packs into `icon_library` table), `import_svg_icons.py` (bulk import SVG packs with pre-rendering). |

## Conventions

- **One-shot scripts stay.** Even if a tool runs once, committing it is cheaper than rediscovering it six months later.
- **Scripts are runnable from the repo root.** `bash tools/scripts/backup.sh`, `python tools/parser/picos_parser.py ...`.
- **Dependencies are documented inline.** If a Python script needs `psycopg2` or `openpyxl`, it's in the top-of-file docstring, not a `requirements.txt` nobody reads.
- **Shell scripts target bash** (not PowerShell) for portability. Windows devs run them via Git Bash or WSL.
- **Nothing in `tools/` is imported from app or service code.** If something under `tools/` grows into a runtime dependency, promote it to `libs/` or inline it into the service that needs it.

## Adding a new tool

Pick the subfolder that matches the tool's domain; create a new one if none fits. Add a one-line description to this README's table.
