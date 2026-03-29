# Service Desk Module

Enterprise service desk integration with ManageEngine SDP On-Demand.
Part of the Central (formerly SwitchBuilder) platform.

## Data Flow Architecture

```
SD Settings Panel (global filters — single source of truth)
    │
    ├─ FiltersChanged ──→ RefreshAllSdAsync(SdFilterState)
    │   ├─ SD Request Grid    (filtered by date, techs, groups)
    │   ├─ SD Overview        (KPI cards + Jira-style chart)
    │   ├─ SD Tech Closures   (per-tech daily bars + target line)
    │   └─ SD Aging           (side-by-side aging buckets per tech)
    │
    └─ GridOptionsChanged ──→ ApplyAllSdGridOptions()
        └─ Group panel, auto-filter, summaries, row colours

Cross-Panel Linking (row selection → auto-filter)
    ├─ SD Technicians  → click row → Request Grid filters by TechnicianName
    ├─ SD Groups       → click row → Request Grid filters by GroupName
    ├─ SD Requesters   → click row → Request Grid filters by RequesterName
    └─ Request Grid    → right-click "Clear Filter" to reset

Chart Drill-Down (double-click → popup grid)
    ├─ Overview bars     → created/closed tickets on that day
    ├─ KPI cards         → matching tickets for the period
    ├─ Tech Closures     → tech's closures on that day
    └─ Aging bars        → tech's open tickets in that age bucket

Group Categories Tree → Save → RefreshSdSettingsAsync()
    └─ Updates SD Settings group filter + request grid combos

Tech Active Toggle → RefreshSdSettingsAsync() + RefreshAllSdAsync()
    └─ Cascades to ALL grids, charts, dropdowns, and filters
```

## Panels

| Panel | Type | Description |
|---|---|---|
| Service Desk | Document | Main request grid — inline editing, dirty tracking, write-back to ME |
| SD Overview | Document | KPI cards (7) + bar/line chart (created vs closed vs avg resolution vs open) |
| SD Tech Closures | Document | Per-tech daily closure bars + expected target line |
| SD Aging | Document | Side-by-side bars per tech (0-1d → 7+d, green → red) |
| SD Groups | Document | CRUD grid for ME support groups (Name, Active, Order) |
| SD Technicians | Document | Grid with Active toggle — disabling cascades everywhere |
| SD Requesters | Document | Read-only grid synced from ME (VIP bool summary) |
| SD Teams | Document | Team management with checked listbox member assignment |
| SD Group Categories | Document | Tree view for nesting ME groups into parent categories |
| SD Settings | Layout (right dock) | Global filters driving all charts + grids |

## SdFilterState (Single Source of Truth)

All SD load methods receive an `SdFilterState` object. No method reads directly from UI controls.

```csharp
public class SdFilterState
{
    DateTime RangeStart, RangeEnd;
    string Bucket;                    // "day", "week", "month"
    List<string>? SelectedTechs;      // null = all
    List<string>? SelectedGroups;     // null = all
    bool ShowOpenLine, ShowResolutionLine, ShowTargetLine;
    bool ShowGroupPanel, ShowAutoFilter, ShowTotalSummary, AlternateRows;

    string FormatLabel(DateTime day);  // bucket-aware label formatting
    static SdFilterState Default();    // "This Week" defaults
}
```

## Engine-Level Patterns Used

| Pattern | Location | Purpose |
|---|---|---|
| `SdFilterState` | Core/Models | Single filter object passed to all queries |
| `MapSdRequest` + `QuerySdRequestsAsync` | Data/DbRepository.ServiceDesk | Shared reader — 1 definition, all queries reuse |
| `LinkSelectionMessage` | Core/Shell/PanelMessageBus | Cross-panel row selection → filter linking |
| `ChartDrillDownWindow` | Desktop/Services | Reusable chart double-click → popup grid |
| `KpiCardBuilder` | Core/Widgets | Reusable KPI cards with trend arrows |
| `TechFilterHelper` | Core/Widgets | Checkbox filter panels with All/None/Team buttons |
| `BoolSummaryHelper` | Module.ServiceDesk/Services | Grid footer counting true values (Active: X / Total: Y) |
| `WireServiceDeskDelegates()` | Desktop/MainWindow | ALL delegates wired once at startup — no boolean guards |

## Delegate Wiring

All SD delegates are wired in `WireServiceDeskDelegates()`, called once from `MainWindow_Loaded`:

- Drill-down events (Overview chart, KPI cards, Tech Closures, Aging)
- CRUD delegates (Teams save/delete, Groups save, Technicians active toggle)
- Group Categories save/delete/structure-changed
- Request grid write-back to ManageEngine
- SD Settings filter/grid option change handlers
- Cross-panel `LinkSelectionMessage` subscription

No boolean guards (`_xxxWired`). No per-load delegate re-assignment.

## ManageEngine Integration

| Setting | Value |
|---|---|
| API Base | `https://sdpondemand.manageengine.eu/api/v3/` |
| OAuth | Zoho EU — `https://accounts.zoho.eu/oauth/v2/token` |
| Auth Header | `Authorization: Zoho-oauthtoken {access_token}` |
| Accept Header | `application/vnd.manageengine.sdp.v3+json` |
| Portal URL | `https://itsupport.immunocore.com` |

### Sync

- Incremental sync via `me_updated_time` watermark
- `fields_required` MUST include `priority`, `urgency`, `impact` (ME omits by default)
- `completed_time` → `resolved_at` (fallback to `updated_time` if null for Resolved status)
- Refresh token auto-rotation (capture new token from Zoho response)
- Default limit: 50,000 requests (500 pages of 100)

### Write-Back

- PUT status, priority, group, technician, category per request
- POST notes to requests
- Dirty row tracking: `AcceptChanges()` → `MarkDirty()` → amber row → Save/Discard

### Status Logic

- "Resolved" and "Closed" both treated as **closed** in all metrics
- Same green colour (#22C55E) for both statuses
- `IsClosed` property: `Status == "Resolved" || Status == "Closed"`
- `IsOverdue` excludes closed, canceled, and archived tickets

## Group Categories

Parent categories map to multiple ME groups for cleaner filtering.

```
sd_group_categories (id, name, sort_order, is_active)
sd_group_category_members (category_id, group_name)
```

Example: "Veeva" category contains 6 ME groups (Veeva Quality, USVeevaCRM, etc.)

SD Settings panel shows category checkboxes — checking "Veeva" selects all child groups.
Uncategorized ME groups appear as individual checkboxes below.

## Tech Active Filtering

`sd_technicians.is_active` controls visibility at query level:

```sql
AND technician_name IN (SELECT name FROM sd_technicians WHERE is_active = true)
```

Applied in: tech names list, tech closures chart, aging chart, KPI active tech count.
Toggle cascades: request grid dropdowns + all chart filters + settings panel.

## Dirty Row Tracking

```
SdRequest.AcceptChanges()    → snapshots originals, starts tracking
SdRequest.MarkDirty()        → compares current vs original, sets IsDirty + amber RowColor
SdRequest.OriginalStatus     → original value for write-back payload diff
RequestGridPanel             → "3 unsaved changes" toolbar + Save/Discard buttons
Save                         → PUT only changed fields to ME API → update local DB → AcceptChanges
Discard                      → revert to original values
```

## KPI Cards

7 cards with trend arrows comparing current vs previous period:

| Card | Metric | Trend Logic |
|---|---|---|
| Incoming | Tickets created in period | Higher = red |
| Closed | Resolved + Closed in period | Higher = green |
| Escalations | High/Urgent priority created | Lower = green |
| SLA Compliant | Closed within due_by | Higher = green |
| Resolution Time | Avg hours (resolved_at - created_at) | Lower = green |
| Open | Currently open tickets | Lower = green |
| Tech:Ticket | Ratio of active techs to open tickets | Display only |

Double-click any card → drill-down popup with matching tickets.

## Database Migrations

| Migration | Tables |
|---|---|
| 033 | `sd_requests`, `sd_requesters`, `sd_technicians`, `integrations`, `integration_credentials`, `integration_log`, permissions, ME seed |
| 034 | `sd_teams`, `sd_team_members` |
| 035 | `resolved_at`, `me_completed_time` columns on `sd_requests` |
| 036 | `sd_groups` lookup, `sd_group_categories`, `sd_group_category_members` |

## Ribbon Layout

```
Service Desk tab
├─ Sync group:        Read, Refresh
├─ Write Back group:  Update Status, Update Priority, Assign Tech, Add Note
├─ View group:        Open Tickets, My Tickets, All Tickets
├─ Dashboards group:  Overview, Tech Closures, Aging
├─ Data group:        Groups, Technicians, Requesters, Teams, Group Categories
└─ Panels group:      SD Settings, Details
```

## Splash Screen

Splash shows during startup, closes before MainWindow appears:
- Data loaded (92%)
- Controls bound (94%)
- Ribbon icons loaded (95%)
- Ribbon built (96%)
- Customization applied (97%)
- Layout restored (98%)
- Then: close splash, set Opacity=1

## Checklist for Adding New SD Features

1. Add DB column/table (migration in `desktop/db/migrations/`)
2. Add model property in `Core/Models/ServiceDeskModels.cs`
3. Add to `MapSdRequest` if it's on `sd_requests`
4. Add repo method using `QuerySdRequestsAsync` (no copy-paste)
5. If it's a filter: add to `SdFilterState`, `SdSettingsPanel`, `RefreshAllSdAsync`
6. If it's a grid panel: add to `ActivePanel` enum + `GetActiveGrid` + panel activation map
7. If it has a bool column: add `SummaryType="Count"` in XAML (Custom summary removed — caused startup deadlock)
8. If it needs cross-panel linking: publish `LinkSelectionMessage` on CurrentItemChanged
9. Wire delegates in `WireServiceDeskDelegates()` — never in a Load method
10. If XAML uses a converter (`StringToBrush`, etc.): define it in `<UserControl.Resources>` locally — NOT inherited from Window
11. Test: change SD Settings → all panels update. Click lookup row → request grid filters.
