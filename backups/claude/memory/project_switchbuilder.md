---
name: Central platform (renamed from Core/SwitchBuilder 2026-03-28)
description: 15-project .NET 10 / PG 18.3 / DX 25.2 / Svg.NET 3.4.7 / Elsa 3.5.3 engine platform — all phases complete, Task module (16 panels, Hansoft clone), 451 tests
type: project
---

Modular WPF desktop + API server platform for FS/PicOS switch config management. First module on a reusable engine. Full TotalLink feature parity plus modern additions (SVG icon system, ribbon customizer, 3-layer icon override).

**Rename history:** SwitchBuilder → Core → Central (completed 2026-03-28). All namespaces, assemblies, DB, env vars now use "Central".

**How to apply:** Follow docs/ARCHITECTURE.md. Build: `dotnet build Central.sln --configuration Release -p:Platform=x64`. Output: `bin/x64/Release/net10.0-windows/`. Check crash.log after launch. Follow feedback_new_module_checklist.md when adding new modules.

## Stack

- .NET 10 (SDK 10.0.201) / Npgsql 10.0.2
- PostgreSQL 18.3 (DB name: `central`, user: `central`)
- DevExpress 25.2 WPF (Grid, Gantt, Charts, Ribbon, Docking)
- Svg.NET 3.4.7 (SVG rendering to WPF ImageSource)
- Elsa Workflows 3.5.3 (workflow engine, PostgreSQL persistence)
- Podman (never Docker)

## Stats (2026-03-29)

- 15 projects in solution (+ Central.Workflows added 2026-03-28)
- 67 DB migrations (055 original + 060-067 task module)
- 451 unit tests (all passing)
- 56+ API endpoints + Swagger + Elsa workflow API
- 16 task module panels (tree, backlog, sprint, burndown, kanban, gantt, QA, QA dashboard, reports, dashboard, timesheet, activity, my tasks, portfolio, import, detail)
- 11,676 Axialist icons (OfficePro + Universal)
- 0 build errors

## Task Module (Hansoft/P4 Plan clone — 11 phases, all complete)

Built 2026-03-28/29. Full project & task management:
- Hierarchy: Portfolio → Programme → Project → Sprint → Epic → Story → Task → Sub-Task
- Backlog with drag-sort priority + commit-to-sprint (reference, not copy)
- Sprint planning with capacity bars + velocity tracking + burndown charts
- Kanban board with WIP limits, swim-lanes, drag-drop status transitions
- DX GanttControl with milestones, baselines, zoom controls
- QA bug tracker with severity/priority separation, batch triage, 4-chart dashboard
- Elsa 3.5.3 workflow engine (6 custom activities, approval bookmarks)
- Custom columns (9 types, field-level permissions, dynamic grid rendering)
- Report builder (visual query builder, reflection-based filter engine)
- Task dashboard (4 charts: status pie, type bar, created line, velocity bar)
- Timesheet (weekly grid, activity types, hour totals)
- Activity feed (auto-populated by PG trigger on task changes)
- My Tasks (personal cross-project view, groupable)
- Portfolio view (hierarchy roll-up with completion %, open bugs, active sprints)
- Import wizard (CSV + MS Project XML parser, column auto-mapping)

## Env Vars

- `CENTRAL_DSN` (primary) / `SWITCHBUILDER_DSN` (legacy fallback)
- Default DSN: `Host=localhost;Port=5432;Database=central;Username=central;Password=central`
