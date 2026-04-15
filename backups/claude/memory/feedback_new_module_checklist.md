---
name: New module + grid checklist
description: CRITICAL — mandatory steps when adding a new module OR a new grid panel. Follow EVERY step or the feature will be incomplete.
type: feedback
---

When adding a NEW MODULE or a NEW GRID PANEL, ALL of these must be done. The Tasks module was added without panel toggles or role permissions — this checklist prevents that. UPDATE THIS FILE when new engine features are added.

**Why:** Multiple times during development, modules were created but couldn't be opened, grids were missing context menus, or features were inconsistent. This checklist is the single source of truth.

**How to apply:** Follow this checklist for EVERY new module/grid. Do NOT skip steps. Do NOT mark a module as complete until every box is checked.

---

## Phase 1: Project Setup

- [ ] Create `Central.Module.{Name}/` project with csproj targeting `net10.0-windows`
- [ ] Add `<ProjectReference>` to Core
- [ ] Add to solution: `dotnet sln add`
- [ ] Add `<ProjectReference>` in `Central.Desktop.csproj`
- [ ] Create `{Name}Module.cs` implementing `IModule, IModuleRibbon, IModulePanels`
- [ ] Set `RequiredPermission` on the ribbon page registration
- [ ] **Add `_ = typeof({Name}Module);` to `Bootstrapper.ForceLoadModuleAssemblies()`** — without this the module DLL won't be JIT-loaded and discovery will miss it
- [ ] Verify `ModuleLoader.DiscoverModules()` finds the module
- [ ] Clear `ribbon_items` + `ribbon_groups` tables to force re-sync on next Ribbon Config panel open

## Phase 2: Database

- [ ] Create migration file in `desktop/db/migrations/` (next number in sequence)
- [ ] Add `pg_notify` trigger on ALL new tables (for real-time SignalR)
- [ ] Seed test data
- [ ] **Add permissions to `permissions` table** (module:read, module:write, module:delete + any custom like module:sync)
- [ ] **Grant ALL permissions to Admin role** (`CROSS JOIN`)
- [ ] **Grant read + write to Operator role**
- [ ] **Grant read to Viewer role**
- [ ] Apply migration: `podman exec -i central-postgres psql -U central -d central -f -`

## Phase 3: Models

- [ ] Create model class(es) in `Central.Core/Models/` with `INotifyPropertyChanged`
- [ ] Add `DetailXxx` `ObservableCollection` if master-detail expansion is needed
- [ ] **Add permission bools to `RoleRecord`** (e.g., `TasksView`, `TasksEdit`, `TasksDelete`)
- [ ] **Update `PermissionSummary` count** in `RoleRecord` (total denominator)
- [ ] Add backing fields + property change notifications for ALL fields

## Phase 4: Repository

- [ ] Add `GetAll{Entity}Async()` method to `DbRepository` (in Data project)
- [ ] Add `Upsert{Entity}Async()` method
- [ ] Add `Delete{Entity}Async()` method
- [ ] Add `Reload{Entity}Async()` method to `MainViewModel`
- [ ] Add `ObservableCollection<T>` to `MainViewModel` for the entity

## Phase 5: UserControl Panel

- [ ] Create `{Name}GridPanel.xaml` + `.xaml.cs` in module `Views/` folder
- [ ] Use `System.Windows.Controls.UserControl` base (not Forms)
- [ ] Define ALL styles inline (no `StaticResource` references to Window resources)
- [ ] Use `clr-namespace:Central.Core.Converters;assembly=Central.Core` for converters
- [ ] Expose `Grid` and `View` properties
- [ ] Wire `ValidateRow` + `InvalidRowException` in constructor
- [ ] Add save delegate event (`Func<T, Task>? SaveXxx`)
- [ ] Add `MasterRowExpanded` handler + `LoadDetailXxx` event if using master-detail
- [ ] **Wire `BoolSummaryHelper.Wire()` for any grid with boolean checkbox columns** — provides custom summary counting true values (Active: X / Total: Y)

## Phase 6: Shell Wiring (MainWindow)

### XAML
- [ ] Add `xmlns:{prefix}Views` for module views namespace
- [ ] Add `<dxdo:DocumentPanel>` in DockLayoutManager with `AllowClose="True"`
- [ ] Place the UserControl inside the DocumentPanel

### MainViewModel
- [ ] Add `bool Is{Name}PanelOpen` property with `OnPropertyChanged()`

### MainWindow.xaml.cs — Constructor Wiring
- [ ] Wire save delegate: `Panel.SaveXxx += async (item) => await VM.SaveXxxAsync(item);`
- [ ] Wire `LoadDetailXxx` event if master-detail (loads on row expand)
- [ ] Add `DockItemClosing` handler: `if (e.Item == Panel) VM.Is{Name}PanelOpen = false;`

### MainWindow.xaml.cs — Panel Toggle
- [ ] Add `PropertyChanged` handler for `Is{Name}PanelOpen`:
  - `ToggleDockPanel(Panel, VM.Is{Name}PanelOpen)`
  - Lazy load data: `if (open) _ = VM.Load{Name}Async();`

### Ribbon
- [ ] **Add `BarCheckItem` panel toggle** to appropriate ribbon Panels group
- [ ] Set `IsChecked="{Binding Is{Name}PanelOpen, Mode=TwoWay}"`

### ActivePanel Registration
- [ ] **Add to `ActivePanel` enum** — every grid panel MUST have an enum value
- [ ] **Add to `GetActiveGrid()` method** — map DocumentPanel → GridControl
- [ ] **Add to panel activation map** — map DocumentPanel activation → ActivePanel enum value

### Startup
- [ ] Add `DockManager.DockController.Close({Name}Panel)` in startup panel close block
- [ ] **Splash screen stays until MainWindow_Loaded completes** (Opacity=0 pattern: ribbon + data + layout fully loaded before window becomes visible)

### State Persistence
- [ ] **Add to `SavePanelStatesAsync` dictionary** (`["{name}"] = VM.Is{Name}PanelOpen`)
- [ ] **Add to panel state restore section** (`if (states.TryGetValue("{name}", out var x)) VM.Is{Name}PanelOpen = x;`)
- [ ] **Add to backstage close-all button** (`VM.Is{Name}PanelOpen = false;`)

## Phase 7: Engine Features (EVERY grid must have ALL of these)

### Context Menu (via `GridContextMenuBuilder.AttachSimple`)
- [ ] CRUD actions (New/Edit/Duplicate/Delete) appropriate to the entity
- [ ] Separator
- [ ] **Bulk Edit Selected** (`BulkEditButton_ItemClick`)
- [ ] **Export to Clipboard** (`ExportGridToClipboard(grid)`)
- [ ] Separator
- [ ] Cross-panel navigation (Go to Switch/Device) if applicable
- [ ] Separator
- [ ] **Refresh** (`_ = VM.Reload{Name}Async()`)

### Master-Detail Expansion (if entity has children or config preview)
- [ ] Add `DataControlDetailDescriptor` in XAML with `ItemsSourcePath="Detail{Children}"`
- [ ] Add `ObservableCollection<TChild> Detail{Children}` to parent model
- [ ] Wire `MasterRowExpanded` → `LoadDetail{Children}` event → populate collection on demand
- [ ] Add `TotalSummary` to detail grid
- [ ] For link grids: use `DetailConfigLines` + `GenerateDetailConfig()` from `NetworkLinkBase` — shows PicOS commands per switch with green monospace font
- [ ] For data grids: load detail from DB on expand (e.g., Switches→interfaces, BGP→neighbors)

### DataModifiedMessage (cross-panel refresh)
- [ ] Publish `DataModifiedMessage` from ALL save methods (Add + Update)
- [ ] Publish `DataModifiedMessage` from ALL delete methods
- [ ] Subscribe in `DataModifiedMessage` handler if this panel depends on other panels' data

### Undo Support
- [ ] Wire `UndoService.Instance.RecordRemove()` in delete method
- [ ] Wire `UndoService.Instance.BeginBatch/CommitBatch` for multi-row deletes

### Grid Features (EVERY grid must have ALL of these)
- [ ] `ShowTotalSummary="True"` with `GridSummaryItem SummaryType="Count"`
- [ ] `GroupSummary` if grid supports grouping
- [ ] `ShowFilterPanelMode="ShowAlways"`
- [ ] `ShowAutoFilterRow="True"` — per-column quick filters
- [ ] `SearchPanelNullText="Search..."` — full-text search across all columns
- [ ] `AllowColumnFiltering="True"`
- [ ] `UseEvenRowBackground="True"` — alternating row colors
- [ ] `Fixed="Left"` on primary identifier column (pin while scrolling)
- [ ] Column widths set explicitly
- [ ] Natural sort on interface/port columns where applicable
- [ ] View toggles work from Home ribbon (Search Panel, Filter Row, Group Panel, Grid Lines)

### Dirty Row Tracking (for write-back grids)
- [ ] `AcceptChanges()` snapshots originals after load
- [ ] Editing tracked fields sets `IsDirty` + visual indicator (e.g., amber `RowColor`)
- [ ] "Save Changes" flushes dirty rows to external API, updates local DB, clears dirty
- [ ] "Discard" reverts to original values via `MarkDirty`/revert pattern

## Phase 8: Dashboard/Chart Features (if module has charts)

### SD Settings Side Panel (if applicable)
- [ ] SD Settings side panel drives all SD charts when visible (global date range, group category, tech filter)
- [ ] When SD Settings panel is hidden, per-chart Expander filter panels take over

### Group Categories
- [ ] Group categories with tree view for nested group filtering (`sd_group_categories` + `sd_group_category_members`)
- [ ] Group filter applied at query level on KPI cards, overview chart, closures chart, request grid

### Chart Drill-Down
- [ ] `ChartDrillDownWindow` for any chart double-click drill-down (engine-level reusable DXDialogWindow)
- [ ] Wire `MouseDoubleClick` or `ChartControl.ObjectHotTracked` → open drill-down with filtered data
- [ ] Available on all chart panels + KPI cards

### Per-Chart Filter Panels
- [ ] Collapsible `Expander` filter panels with `TechFilterHelper` (All/None/Team quick-select)
- [ ] Date range picker
- [ ] Team/tech checkbox filters

## Phase 9: API

- [ ] Create `{Name}Endpoints.cs` in `Central.Api/Endpoints/`
- [ ] Map GET (list), GET by ID, POST (create), PUT (update), DELETE
- [ ] Wire in `Program.cs`: `app.MapGroup("/api/{name}").Map{Name}Endpoints().RequireAuthorization();`
- [ ] Add to Swagger tags

## Phase 10: SignalR Real-Time

- [ ] Add table name to `SignalR DataChanged` handler in MainWindow
- [ ] Map table name → `VM.Reload{Name}Async()` call
- [ ] Toast notification on external change

## Phase 11: Icons & Ribbon

### Icon Picker (if module has ribbon buttons with icons)
- [ ] Register ribbon items in `ribbon_items` DB table (migration 032) with `glyph` name
- [ ] Use `ImagePickerWindow` for icon selection (returns icon ID + name)
- [ ] Wire `OpenIconPicker` delegate on any tree/grid panels that allow icon customization
- [ ] Set `RenderIconPreview` delegate to render icon name → ImageSource preview
- [ ] Verify 3-layer override resolves correctly: user_ribbon_overrides > ribbon_items > admin_ribbon_defaults
- [ ] Verify `PreloadIconOverridesAsync` includes the new ribbon items (no flash on startup)
- [ ] SVG icons: if importing custom SVGs, use `IconService.ImportSvgAsync()` — max 20,000 icons

### Ribbon Tree Customizer
- [ ] New ribbon pages/groups/items appear in user's RibbonTreePanel
- [ ] New ribbon pages/groups/items appear in admin's RibbonAdminTreePanel
- [ ] Admin can set default icon, label, display style (large/small/smallNoText) for new items
- [ ] Admin can set link targets (panel:PanelName, url:..., action:..., page:...)
- [ ] User can override icon, text, visibility for new items
- [ ] Auto-save fires on icon pick, hide toggle, rename (SaveSingleOverride delegate)
- [ ] Push All Defaults propagates admin changes to all users

## Phase 12: Verify

- [ ] Build: `dotnet build Central.sln --configuration Release -p:Platform=x64` — 0 errors
- [ ] Launch: no crash.log entries
- [ ] Panel opens from ribbon toggle
- [ ] Data loads
- [ ] CRUD works (add/edit/delete)
- [ ] Context menu appears on right-click with ALL items
- [ ] Master-detail expands (if applicable)
- [ ] Undo works after delete
- [ ] Saved filters work
- [ ] Export to clipboard works
- [ ] Panel state saves/restores on restart
- [ ] Role permissions gate the panel correctly
- [ ] API endpoints return data
- [ ] Ribbon items show correct icons (3-layer override)
- [ ] Icon picker works for new ribbon items
- [ ] Ribbon tree customizer shows new items in correct hierarchy
- [ ] ActivePanel enum includes all new grid panels
- [ ] BoolSummaryHelper wired on boolean columns
- [ ] Splash screen holds until MainWindow_Loaded completes
