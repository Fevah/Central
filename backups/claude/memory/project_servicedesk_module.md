---
name: Service Desk Module
description: SD module — ManageEngine sync, dashboards, teams, group categories, dirty tracking, write-back, settings panel, drill-down charts
type: project
---

## Service Desk Module (Central.Module.ServiceDesk)

Integrates with ManageEngine SDP On-Demand.

### Panels (all dockable)
- **Service Desk** — main request grid, inline editing with dirty tracking + write-back
- **SD Overview** — KPI cards + Jira-style bar/line chart (created vs closed vs avg resolution vs open). Point-in-time open count (not current status). Period-wide mean resolution line (flat, not per-day spikes). Drill-down on chart bars and KPI cards via ChartDrillDownWindow.
- **SD Tech Closures** — per-tech daily closures with expected target line, filter by tech/team. Drill-down on chart bars.
- **SD Aging** — side-by-side bars per tech (0-1d green → 7+d red), filter by tech/team. Drill-down on chart bars.
- **SD Groups** — CRUD grid for support groups (Name, Active, Order)
- **SD Technicians** — grid with Active toggle, disabling cascades to all grids/charts
- **SD Requesters** — read-only grid synced from ME (VIP bool summary)
- **SD Teams** — team management with checked listbox member assignment
- **SD Settings** — side panel with global filters (date range, group category, tech filter) that drive all SD charts/grids when visible

### Group Categories
- `sd_group_categories` table defines parent categories (e.g., "Infrastructure", "Applications")
- `sd_group_category_members` table maps ME group names to parent categories
- Tree view UI for nested group filtering
- Group filter applied at query level on KPI cards, overview chart, closures chart, request grid

### Chart Drill-Down
- `ChartDrillDownWindow` — engine-level reusable DXDialogWindow
- Double-click any chart bar or KPI card opens drill-down with filtered request grid
- Available on all 3 charts (Overview, Tech Closures, Aging) + all KPI cards

### ManageEngine Integration
- OAuth2 via Zoho EU (`accounts.zoho.eu`), refresh token auto-rotation
- All config from DB: `integrations` table (base_url, config_json) + `integration_credentials` (encrypted)
- Incremental sync via `me_updated_time` watermark + `search_criteria` filter
- `fields_required` must explicitly request `priority`, `urgency`, `impact`
- `completed_time` → `resolved_at` (NOT `synced_at`)
- Status "Resolved" and "Closed" both treated as "closed" in all metrics
- Write-back: PUT status/priority/group/technician/category + POST notes

### Key Patterns
- `BoolSummaryHelper.Wire()` — custom DX grid summary counting true values
- `TechFilterHelper.BuildFilter()` — checkbox panel with All/None/Team quick-select
- `KpiCardBuilder.BuildCards()` — reusable KPI card strip with trend arrows
- `ChartDrillDownWindow` — reusable chart drill-down for any chart double-click

### Migrations
- 033: SD tables, integrations, permissions, ME seed
- 034: sd_teams + sd_team_members
- 035: resolved_at + me_completed_time
- 036: sd_groups lookup table + sd_group_categories + sd_group_category_members
