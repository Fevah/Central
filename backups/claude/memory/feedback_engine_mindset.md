---
name: Engine mindset — all features needed, modernised, consistent
description: CRITICAL — Central is an ENGINE platform. Every TotalLink capability is needed, modernised for .NET 10, and CONSISTENT across all panels.
type: feedback
---

Central is a **modular engine platform**. Every capability from TotalLink is needed because future modules will use them. But TotalLink is legacy code — the patterns must be **modernised** for .NET 10, not copied verbatim.

**Why:** User corrected me for (1) dismissing features as "overkill", (2) the source is legacy that needs modernising, and (3) implementing the same feature differently in different panels.

**How to apply:**

### Never dismiss features
- NEVER say "overkill" or "not needed" about TotalLink features
- If TotalLink has it, the engine needs it — frame as "build now" vs "build next", never "not needed"
- Full CRUD admin panels ARE needed for: ribbon items, panel config, module settings, permissions, roles, users, themes
- DB-backed configuration IS needed for: ribbon structure, panel layout, user preferences, module settings

### Always consistent — same engine pattern everywhere
- If a feature exists in one grid, it must use the SAME engine pattern in ALL grids
- NEVER implement the same feature differently in different panels (e.g., master-detail must use DataControlDetailDescriptor everywhere, not split grid in one place and detail descriptor in another)
- Global functions (print, export, filter, column chooser, context menu, master-detail) must be available on EVERY grid
- One pattern for everything — modules consume engine features, they don't reinvent them

### Always modernise
- TotalLink uses WCF → we use REST + SignalR
- TotalLink uses XPO ORM → we use raw Npgsql (lightweight, no ORM overhead)
- TotalLink uses FormsAuthentication → we use JWT
- TotalLink uses Settings.Default → we use DB-backed user_settings
- TotalLink uses Autofac everywhere → we use Autofac for module discovery, keep DI simple
- TotalLink uses AutoMapper for ribbon → we use direct POCO mapping (less magic)
- TotalLink uses ChangeFactory + MonitoredUndo → we use simpler undo with DB audit log
- TotalLink uses FacadeBase dual Data+Method → we use single IDataService abstraction
- Replace XML config with JSON/DB config
- Replace binary serialization with JSON serialization
- Use async/await throughout (TotalLink has sync-over-async patterns)
- Use C# 12/13 features (primary constructors, collection expressions, pattern matching)
- Use record types for immutable DTOs
- Target .NET 10 + PostgreSQL 18 + Npgsql 10
