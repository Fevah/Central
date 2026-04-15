---
name: Modular platform engine
description: Central is first module on a reusable WPF engine — ListViewModelBase, ImporterBase, SettingsProvider, PanelMessageBus, StartupWorkerManager
type: project
---

Central is the FIRST MODULE on a reusable WPF platform engine. The engine is in `Central.Core` and `Central.Desktop` (shell). Any future module inherits all enterprise features by extending base classes.

**Why:** Building an engine, not just a switch app. Every grid gets context menu, export, print, duplicate, summaries for free. Modules declare; engine provides.

**How to apply:**
- ALWAYS read `docs/ARCHITECTURE.md` §17 before building new features
- New grids: extend `ListViewModelBase<T>`
- New imports: extend `ImporterBase<T>`
- New settings: call `ISettingsProvider.Register()`
- Cross-panel: use `PanelMessageBus.Publish()`
- Context menus: call `GridContextMenuBuilder.AttachSimple()`
- Startup work: extend `StartupWorkerBase`

## Engine components (Core + Data + Shell)

| Class | What module authors get for free |
|-------|--------------------------------|
| `ListViewModelBase<T>` | CRUD commands, duplicate, export clipboard, context menu, auto-ribbon |
| `ImporterBase<T>` | Validation, progress bar, cancel, error navigation, summary |
| `ISettingsProvider` | Per-user DB settings, backstage Settings tab auto-renders |
| `StartupWorkerManager` | Splash screen pipeline with progress |
| `PanelMessageBus` | Selection, Navigate, DataModified, Refresh messages |
| `GridContextMenuBuilder` | One-line right-click menus for any grid |
| `PasswordHasher` | SHA256 + salt authentication |
| `IconService` | Singleton metadata cache, admin/user icon resolution, search, bulk SVG import |
| `SvgHelper` | SVG→WPF ImageSource via Svg.NET, currentColor→white, memory + disk cache |
| `ImagePickerWindow` | DXDialogWindow icon picker — pack/category filter, async batch PNG load |
| `RibbonTreeItem` | Flat tree node model for ribbon customizer (Id/ParentId, display style, link target) |

## DB migrations for engine

| Migration | What |
|-----------|------|
| 024 | 25 permission codes, role_permission_grants |
| 025 | audit_log (JSONB), soft delete columns |
| 026 | pg_notify triggers on 19 tables |
| 027 | user auth fields (password_hash, salt, user_type, last_login) |
| 028 | default_user_settings table + auto-seed trigger |
