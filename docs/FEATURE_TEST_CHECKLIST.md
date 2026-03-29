# Central Feature Test Checklist

Comprehensive test checklist for the Central (formerly SwitchBuilder) desktop + API platform.
Every checkbox is a manually testable item.

---

## 1. Application Startup

- [ ] App launches without crash (check crash.log)
- [ ] Splash screen appears with progress bar
- [ ] Startup workers execute in order (DB connect, load data, init modules)
- [ ] Windows auto-login succeeds (matches Windows username to app_users)
- [ ] LoginWindow appears when auto-login fails (wrong/missing username)
- [ ] Offline mode activates when DB is unreachable (5s timeout)
- [ ] Status bar shows "Offline" when DB is down
- [ ] Auto-reconnect fires after DB comes back (10s retry)
- [ ] Data loads automatically after reconnect
- [ ] ConnectivityManager fires ConnectionChanged event
- [ ] XAML layout errors recovered gracefully (toast shown, not crash/hang)
- [ ] Missing resource in module panel shows warning, doesn't deadlock
- [ ] crash.log + startup.log written on errors

## 2. Authentication & RBAC

### Login
- [ ] Windows auto-login populates UserSession.CurrentUser
- [ ] LoginWindow accepts valid username/password
- [ ] LoginWindow rejects invalid credentials with error message
- [ ] Account lockout activates after 5 failed attempts
- [ ] Account lockout expires after 15 minutes
- [ ] Set Password dialog (SHA256 + salt) works from backstage

### Roles & Permissions
- [ ] Admin role sees all ribbon tabs, all panels, all grid editing
- [ ] Operator role sees permitted tabs, can edit but not delete (where configured)
- [ ] Viewer role sees permitted tabs, grid editing disabled
- [ ] Custom roles respect per-module permissions (View/Edit/Delete)
- [ ] Ribbon buttons hidden for denied permissions (IsVisible binding)
- [ ] Panels closed for denied permissions (DockController.Close)
- [ ] Grid inline editing disabled for read-only roles

### Site-Level Access
- [ ] role_sites controls which buildings each role sees
- [ ] IPAM grid only shows devices from allowed sites
- [ ] Switches grid only shows switches from allowed sites
- [ ] SQL-level filtering (WHERE building = ANY(@sites)) confirmed

## 3. Backstage

- [ ] User Profile tab shows current user info
- [ ] Settings tab shows DB-backed per-user settings
- [ ] Theme gallery shows 9 installed themes
- [ ] Theme changes apply immediately (DX ThemeManager)
- [ ] Theme persists across restarts (saved to user_settings)
- [ ] Connection tab shows DB connection status
- [ ] About tab shows version and build info
- [ ] Switch User button opens LoginWindow
- [ ] Exit button closes application
- [ ] API URL setting in backstage saves correctly
- [ ] Auto-connect toggle persists and works on next startup

## 4. Ribbon

### General
- [ ] All ribbon tabs render without errors
- [ ] ItemClick events fire on button click
- [ ] Ribbon page headers display correctly
- [ ] Settings cog (BarSubItem) appears in page headers
- [ ] Save Layout / Restore Default work from settings cog

### Context Tabs
- [ ] Links tab (blue) appears when Links panel is active
- [ ] Switch tab (green) appears when Switches panel is active
- [ ] Admin tab (amber) appears when Admin panel is active
- [ ] Context tabs hide when switching to unrelated panel

### Quick Access Toolbar
- [ ] Save/Refresh/Undo buttons appear in QAT
- [ ] QAT buttons are functional
- [ ] QAT is user-customizable (right-click add/remove)

### Ribbon Customization (3-layer override)
- [ ] User RibbonTreePanel opens from Admin ribbon
- [ ] Tree shows hierarchy: pages > groups > items
- [ ] Icon picker button opens ImagePickerWindow
- [ ] Selected icon appears in tree preview
- [ ] Custom text overrides default label
- [ ] Hide/Show toggle works (IsHidden property)
- [ ] Move Up/Down reorders items
- [ ] Apply button saves all overrides
- [ ] Reset All clears user overrides
- [ ] Auto-save fires on icon pick (SaveSingleOverride)
- [ ] Auto-save fires on hide toggle
- [ ] Admin RibbonAdminTreePanel opens from Admin ribbon
- [ ] Admin can set default icon for all users
- [ ] Admin can set display style (large/small/smallNoText)
- [ ] Admin can set link target (panel/url/action/page)
- [ ] Admin can add new pages/groups/items
- [ ] Admin can add separators
- [ ] Push All Defaults propagates to all users
- [ ] PreloadIconOverridesAsync prevents icon flash on startup
- [ ] 3-layer resolution: user_ribbon_overrides > ribbon_items > admin_ribbon_defaults

## 5. Icon System

### Icon Library
- [ ] IconService loads metadata on startup (11,676 icons)
- [ ] IconService.AllIcons contains OfficePro + Universal packs
- [ ] IconService.GetCategories() returns distinct category list
- [ ] IconService.GetIconSets() returns ["OfficePro", "Universal"]
- [ ] IconService.Search() filters by name, category, size

### ImagePickerWindow
- [ ] Window opens as DXDialogWindow (themed)
- [ ] Pack checkboxes show OfficePro + Universal
- [ ] All packs selected by default
- [ ] Category checkboxes populate from selected packs
- [ ] Changing pack selection updates category list
- [ ] No icons load until categories are selected
- [ ] Select All Categories button selects all
- [ ] Clear All Categories button clears all
- [ ] Search box filters icons by name
- [ ] Icons load asynchronously (Loading... indicator)
- [ ] Generation counter cancels stale loads on rapid filter change
- [ ] Icons render from pre-rendered PNG 32px (not live SVG)
- [ ] Count label shows "N icons" or "N of M icons (narrow with search)"
- [ ] Select button returns icon ID + name
- [ ] Double-click selects icon
- [ ] Clear button returns -1 (explicitly cleared)
- [ ] Delete button removes icon from DB after confirmation

### SVG Rendering
- [ ] SvgHelper.RenderSvgToImageSource renders SVG to BitmapImage
- [ ] currentColor replaced with #FFFFFF for dark theme
- [ ] In-memory cache (hash-keyed) prevents re-rendering
- [ ] Disk cache writes to %LocalAppData%/Central/icon_cache/
- [ ] LoadFromDiskCache reads cached SVG files

## 6. IPAM Panel (Devices)

### Grid
- [ ] DeviceRecord grid loads all devices from DB
- [ ] Inline editing works (NavigationStyle=Cell)
- [ ] Dropdown columns wired via BindComboSources()
- [ ] RESERVED rows highlighted with distinct background
- [ ] ValidateRow auto-saves on row commit
- [ ] TotalSummary shows device count
- [ ] GroupSummary shows count per group
- [ ] ShowFilterPanelMode="ShowAlways" visible
- [ ] ShowAutoFilterRow visible
- [ ] Search panel works (full-text)
- [ ] Column filtering works
- [ ] Alternating row colors (UseEvenRowBackground)
- [ ] Primary column frozen (Fixed=Left)
- [ ] Natural numeric sort on interface columns

### CRUD
- [ ] New device creates empty row
- [ ] Edit device modifies existing row
- [ ] Delete device removes row (with confirmation)
- [ ] Duplicate device creates copy
- [ ] Undo after delete restores row

### Context Menu
- [ ] Right-click shows context menu
- [ ] New/Edit/Duplicate/Delete actions work
- [ ] Bulk Edit Selected opens BulkEditWindow
- [ ] Export to Clipboard copies grid data
- [ ] Cross-panel navigation (Go to Switch) works
- [ ] Refresh reloads data

### Master-Detail
- [ ] Row expansion shows detail data
- [ ] Detail grid has TotalSummary

## 7. Switches Panel

### Grid
- [ ] SwitchRecord grid loads all switches
- [ ] Ping status icons (green/red/grey circles) display correctly
- [ ] SSH status icons display correctly
- [ ] Latency column shows ping time
- [ ] INotifyPropertyChanged updates icons in real-time

### Connectivity
- [ ] Ping All button pings all switches in parallel
- [ ] Ping Selected button pings selected switches
- [ ] Sync BGP button syncs BGP config from live switch
- [ ] Sync All BGP syncs all switches

### Context Menu
- [ ] All standard context menu items present
- [ ] Go to Device navigates to IPAM panel

## 8. Links Panels (P2P, B2B, FW)

### P2P Links
- [ ] Grid loads point-to-point links
- [ ] Inline editing works
- [ ] Multi-column port dropdowns show Interface, Admin, Link, Speed, Description, LLDP
- [ ] Master-detail shows PicOS config preview (DetailConfigLines)
- [ ] Config preview uses green monospace font
- [ ] DataModifiedMessage fires on save

### B2B Links
- [ ] Grid loads back-to-back links
- [ ] BuildConfig generates `set protocols bgp neighbor ... remote-as ... bfd`
- [ ] Config does NOT emit `port-mode "trunk"`

### FW Links
- [ ] Grid loads firewall links
- [ ] Config does NOT emit `port-mode "trunk"`

## 9. BGP Panel (Routing)

- [ ] Top grid shows BGP config per switch (AS, router-id, settings)
- [ ] Master-detail: bottom tabs show Neighbors + Advertised Networks
- [ ] SSH sync downloads live BGP config
- [ ] fast_external_failover, bestpath_multipath_relax columns editable
- [ ] last_synced timestamp updates after sync

## 10. VLANs Panel

- [ ] VLAN grid loads all VLANs
- [ ] Standard grid features (summary, filter, search, export)

## 11. Tasks Panel

- [ ] Task grid loads with tree hierarchy
- [ ] Tree nodes expand/collapse
- [ ] CRUD operations work

## 12. Admin Panels

### Users Panel
- [ ] User grid loads from app_users
- [ ] Role dropdown bound to DB roles
- [ ] New user creates record
- [ ] Edit user modifies record
- [ ] Delete user removes record

### Roles & Permissions Panel
- [ ] Split view: roles grid left, permissions tree + site checkboxes right
- [ ] Permissions tree shows 25 module:action codes
- [ ] Checkbox toggles grant/revoke permission
- [ ] Site access checkboxes control building access

### Lookup Values Panel
- [ ] Category/Value grid loads
- [ ] CRUD works
- [ ] SortOrder column respected

### SSH Logs Panel
- [ ] SSH session logs display
- [ ] Filterable by switch/date

### App Logs Panel
- [ ] Application log entries display

### Jobs Panel
- [ ] Job schedules grid shows 3 job types
- [ ] Enable/Disable toggle works
- [ ] Interval column editable
- [ ] Run Now button triggers immediate execution
- [ ] Job history grid shows past runs

### Ribbon Config Panel
- [ ] Ribbon pages/groups/items display in flat grid
- [ ] CRUD on ribbon items works
- [ ] System items cannot be deleted (is_system=TRUE)

## 13. Config Compare Panel

- [ ] Side-by-side diff view with line numbers
- [ ] Pink highlighting on changed lines
- [ ] Synced scroll between left and right panels
- [ ] Compare button toggles panel from Details > Config tab

## 14. Detail Panel (Asset Details)

- [ ] Right-docked detail panel visible
- [ ] Updates on grid row selection (SelectionChanged)
- [ ] Shows correct detail fields for selected entity

## 15. Cross-Panel Features

### DataModifiedMessage
- [ ] Save device → links panel refreshes
- [ ] Save switch → related panels refresh
- [ ] Delete entity → dependent panels refresh

### Panel Navigation
- [ ] Go to Switch A/B from Links context menu
- [ ] Go to Device from Switches context menu
- [ ] Navigation activates target panel + selects row

### Saved Filters
- [ ] Save current filter with name
- [ ] Load saved filter applies expression
- [ ] Delete saved filter removes it
- [ ] Filters are per-user per-panel

## 16. Layout Persistence

- [ ] Grid column widths save/restore
- [ ] Grid column order saves/restores
- [ ] Grid column visibility saves/restores
- [ ] Dock panel positions save/restore
- [ ] Window bounds (size, position) save/restore
- [ ] Panel open/close states save/restore
- [ ] Restore Default resets to XAML defaults
- [ ] Layout saved to user_settings table per user

## 17. Export & Print

- [ ] Export to Clipboard works from context menu
- [ ] Export to Clipboard works from ribbon button
- [ ] Print Preview opens for active grid
- [ ] Column Chooser opens for active grid

## 18. View Toggles (Home Ribbon)

- [ ] Search Panel toggle shows/hides search
- [ ] Filter Row toggle shows/hides auto-filter row
- [ ] Group Panel toggle shows/hides group area
- [ ] Grid Lines toggle shows/hides cell borders
- [ ] Best Fit auto-sizes columns

## 19. Undo/Redo

- [ ] Undo button reverts last operation
- [ ] Redo button re-applies undone operation
- [ ] Split button dropdown shows last 10 operations
- [ ] Delete records can be undone (RecordRemove)
- [ ] Multi-row delete uses BeginBatch/CommitBatch

## 20. Bulk Edit

- [ ] BulkEditWindow opens from context menu
- [ ] Field picker shows all editable fields (reflection-based)
- [ ] Preview shows changes before applying
- [ ] Apply updates all selected rows
- [ ] Works with any model type

## 21. Toast Notifications

- [ ] Info toast (blue) auto-hides after 4s
- [ ] Success toast (green) auto-hides after 4s
- [ ] Warning toast (amber) auto-hides after 4s
- [ ] Error toast (red) auto-hides after 4s
- [ ] Toast appears bottom-right
- [ ] SignalR DataChanged triggers toast for external changes

## 22. API Server

### REST Endpoints
- [ ] GET /api/devices returns device list
- [ ] POST /api/devices creates device
- [ ] PUT /api/devices/{id} updates device
- [ ] DELETE /api/devices/{id} deletes device
- [ ] GET /api/switches returns switch list
- [ ] GET /api/links returns link list
- [ ] GET /api/vlans returns VLAN list
- [ ] GET /api/bgp returns BGP config list
- [ ] GET /api/admin/users returns user list
- [ ] GET /api/jobs returns job schedules

### SSH Endpoints
- [ ] POST /api/ssh/{id}/ping pings switch
- [ ] POST /api/ssh/{id}/download-config downloads config
- [ ] POST /api/ssh/{id}/sync-bgp syncs BGP
- [ ] POST /api/ssh/ping-all batch pings

### Authentication
- [ ] POST /api/auth/login returns JWT token
- [ ] Bearer token required on all endpoints
- [ ] 25 permission claims in token
- [ ] 401 on expired/invalid token
- [ ] Auto token refresh on 401

### SignalR
- [ ] NotificationHub connects on startup
- [ ] DataChanged event fires on DB changes (pg_notify)
- [ ] PingResult event fires on ping completion
- [ ] SyncProgress event streams during SSH operations
- [ ] WPF grids auto-refresh on SignalR events

### Swagger
- [ ] /swagger loads OpenAPI UI
- [ ] All endpoints documented

## 23. Background Jobs

- [ ] JobSchedulerService checks every 30s
- [ ] ping_scan runs every 10 minutes when enabled
- [ ] config_backup runs every 24 hours when enabled
- [ ] bgp_sync runs every 6 hours when enabled
- [ ] Admin can enable/disable jobs via API
- [ ] Admin can change job intervals
- [ ] Admin can trigger immediate run
- [ ] Job history records execution results

## 24. Database

### Connection
- [ ] DSN from CENTRAL_DSN env var works
- [ ] Fallback DSN (localhost defaults) works
- [ ] 5s connection timeout
- [ ] 10s background retry on failure
- [ ] pg_notify triggers fire on all 19+ tables

### Migrations
- [ ] All 32 migrations apply cleanly on fresh DB
- [ ] Migrations are idempotent (re-run safe)

## 25. Web App

- [ ] Dashboard loads at /
- [ ] IPAM grid loads at /ipam (987 devices)
- [ ] Switch detail loads at /switches/{hostname}
- [ ] Config preview loads at /switches/{hostname}/preview
- [ ] Guide loads at /guide
- [ ] Guide detail at /guide/{id} — connection toggles work (HTMX)
- [ ] Import page at /import — .txt and .xlsx upload
- [ ] Running configs at /switches/{hostname}/running-configs
- [ ] HTTP Basic Auth (admin/admin default)
- [ ] Ping button on switch detail works
- [ ] Test SSH button works
- [ ] Download Running Config button works

---

## 26. Service Desk Module

### ManageEngine Sync
- [ ] Read (Sync) button pulls tickets from ManageEngine
- [ ] Incremental sync only pulls changed records (not full re-sync)
- [ ] Priority, urgency, impact fields populated (not blank)
- [ ] resolved_at populated from completed_time (not synced_at)
- [ ] Refresh token auto-rotates if Zoho returns new token
- [ ] Auth failure shows error toast (not silent "up to date")
- [ ] Sync status updates in status bar during pull

### SD Request Grid
- [ ] Grid loads with data, sorted by created date descending
- [ ] Status column shows colour-coded text (Open=blue, Closed=green, etc.)
- [ ] Priority column shows colour-coded text
- [ ] Overdue icon (!) shows for open tickets past due date
- [ ] "Open" hyperlink column opens ticket in browser
- [ ] Inline editing works for Status (dropdown), Priority (dropdown)
- [ ] Inline editing works for Group (dropdown), Technician (dropdown), Category (dropdown)
- [ ] Editing a field turns row amber (dirty tracking)
- [ ] "3 unsaved changes" count shows in toolbar
- [ ] Save Changes button writes to ManageEngine API
- [ ] Discard button reverts to original values
- [ ] Context menu: Read, Update Status/Priority, Assign Tech, Add Note
- [ ] Context menu: Open in Browser, Clear Filter, Export, Refresh
- [ ] Total summary count at bottom of grid

### SD Overview Dashboard
- [ ] KPI cards show: Incoming, Closed, Escalations, SLA Compliant, Resolution Time, Open, Tech:Ticket
- [ ] KPI trend arrows show comparison vs previous period
- [ ] Double-click KPI card opens drill-down grid with matching tickets
- [ ] Bar chart shows created (dark red) vs closed (olive green) per bucket
- [ ] Avg resolution days line is flat (period-wide mean, not per-day spikes)
- [ ] Open issues line shows point-in-time count (drops when tickets close)
- [ ] Double-click a bar opens drill-down grid for that day
- [ ] Summary text shows totals + closure rate %

### SD Tech Closures
- [ ] Bar chart shows per-tech daily closures
- [ ] Expected target dashed line visible
- [ ] Double-click a bar opens drill-down grid (tech's closures that day)

### SD Aging
- [ ] 5 side-by-side bars per tech (0-1d green → 7+ red)
- [ ] Double-click a bar opens drill-down grid (tech's open tickets in that age bucket)

### SD Settings Panel (Global Filters)
- [ ] Time Range dropdown changes all chart date ranges
- [ ] Time Scale dropdown changes bucket size (day/week/month)
- [ ] Technician checkboxes filter closures + aging charts
- [ ] All/None buttons for tech checkboxes work
- [ ] Team buttons select only that team's members
- [ ] Group category checkboxes filter overview + request grid
- [ ] Uncategorized groups show as individual checkboxes
- [ ] Open Issues Line toggle shows/hides line on overview
- [ ] Avg Resolution Line toggle shows/hides line on overview
- [ ] Grid Display options (Group Panel, Auto Filter, Summaries, Row Colours) apply to all SD grids
- [ ] Apply button triggers refresh
- [ ] Reset button restores defaults

### SD Groups
- [ ] Grid shows all ME groups with Active checkbox
- [ ] Inline editing group name + sort order, auto-saves on row commit
- [ ] Active: X / Total: Y summary at bottom
- [ ] Disabling a group removes it from request grid dropdown

### SD Technicians
- [ ] Grid shows all ME technicians with Active checkbox
- [ ] Toggle Active auto-saves to DB
- [ ] Active: X / Total: Y summary at bottom
- [ ] Disabling a tech removes from all dropdowns, charts, and filters
- [ ] Cascade: disabling refreshes request grid combos + chart filter panels

### SD Requesters
- [ ] Grid shows all ME requesters (read-only, synced)
- [ ] VIP: X / Total: Y summary at bottom

### SD Teams
- [ ] Teams grid: add/edit/delete teams
- [ ] Right panel: checked listbox of all technicians
- [ ] Check/uncheck assigns techs to team, auto-saves
- [ ] Team buttons appear in chart filter panels

### Group Categories
- [ ] Tree view shows parent categories with nested ME groups
- [ ] Drag groups under categories to nest
- [ ] Add Category button creates new category node
- [ ] Delete button removes category (children move to root)
- [ ] Save button persists to DB + refreshes SD Settings filter
- [ ] Category shows in SD Settings as gold checkbox with count
- [ ] Checking category selects all child groups in filter
- [ ] Tooltip on category checkbox shows member list

### Cross-Panel Linking
- [ ] Click technician in SD Technicians → Request grid auto-filters to that tech
- [ ] Click group in SD Groups → Request grid auto-filters to that group
- [ ] Click requester in SD Requesters → Request grid auto-filters to that requester
- [ ] Right-click "Clear Filter" on Request grid resets the filter
- [ ] Service Desk panel activates when linked filter applies

### Write-Back to ManageEngine
- [ ] Update Status writes correct value to ME API
- [ ] Update Priority writes correct value to ME API
- [ ] Assign Technician writes correct value to ME API
- [ ] Add Note posts to ME API
- [ ] Bulk dirty row save writes all changed rows
- [ ] Write-back errors show in status bar + app log
- [ ] Integration log records sync/write actions with duration

---

## 27. Service Desk Infrastructure

- [ ] ManageEngine integration record exists in integrations table
- [ ] OAuth credentials stored encrypted in integration_credentials
- [ ] config_json has oauth_url + portal_url
- [ ] sd_groups seeded from sd_requests distinct group_name
- [ ] sd_group_categories + members tables exist
- [ ] sd_teams + members tables exist
- [ ] resolved_at + me_completed_time columns on sd_requests
- [ ] v_master_devices view exists
- [ ] All SD permissions granted to Admin role (servicedesk:read/write/sync/delete)
- [ ] DB pod running with 8GB memory + PG tuning

---

## 28. Extended User Management

- [ ] Extended user fields (department, title, phone, mobile, company) visible in Users grid
- [ ] UserType dropdown shows all 5 types (System, Admin, Standard, ActiveDirectory, Service)
- [ ] Protected users (System, Service) cannot be deleted — delete returns false, shows message
- [ ] Inactive users show at 0.5 opacity, protected users show bold
- [ ] SecureString password handling in LoginWindow

## 29. Active Directory Integration

- [ ] AD Browser panel opens from Admin > Panels > AD Browser
- [ ] Browse AD button queries configured domain via System.DirectoryServices.AccountManagement
- [ ] AD users shown in read-only grid with ObjectGuid, DisplayName, Email, Department, Enabled
- [ ] IsImported column shows which AD users are already linked
- [ ] Import Selected creates app_users with user_type=ActiveDirectory, ad_guid linked
- [ ] Sync All updates display name, email, phone, active status from AD
- [ ] AD config stored in integrations table (domain, OU filter, service account)

## 30. Schema Migration Management

- [ ] Migrations panel shows all applied (green) and pending (amber) migrations
- [ ] Applied migrations show timestamp and duration
- [ ] Apply Pending button runs all pending .sql files in transaction
- [ ] Migration history recorded in migration_history table
- [ ] Refresh reloads from DB + filesystem

## 31. Database Backup & Restore

- [ ] Backup panel with Full Backup button and output path browser
- [ ] pg_dump runs with connection params from DSN
- [ ] Backup history grid shows type, file path, size, status, timestamp
- [ ] Scheduled backup via db_backup job type in job_schedules
- [ ] Failed backups logged with error message

## 32. Soft-Delete Purge

- [ ] Purge panel shows tables with soft-deleted record counts
- [ ] Purge Selected deletes from one table, Purge All clears all
- [ ] Confirmation dialog before purge
- [ ] Count refreshes after each purge operation

## 33. Location Management

- [ ] Locations panel with Countries grid (Code, Name, SortOrder)
- [ ] Regions grid filtered by selected country
- [ ] Add/Delete/Save for both countries and regions
- [ ] Seed data: GBR, USA, AUS, NZL

## 34. Reference Number System

- [ ] Reference Config panel shows entity types with prefix/suffix/pad/next value
- [ ] SampleOutput column shows live preview (e.g. DEV-000001)
- [ ] Auto-save on cell edit
- [ ] next_reference() PG function for atomic sequence generation
- [ ] Seeded: device, ticket, asset, task

## 35. Podman Container Management

- [ ] Podman panel shows containers with Name, Image, State, Status
- [ ] Start/Stop/Restart buttons for selected container
- [ ] View Logs button shows last 100 lines in text area
- [ ] Refresh reloads container list
- [ ] Graceful handling if podman not installed

## 36. Panel Customizer Framework

- [ ] GridCustomizerDialog with row height, alternating rows, summary footer, group panel, auto-filter
- [ ] panel_customizations table stores per-user per-panel settings as JSONB
- [ ] Settings: grid, filter, form, link types

## 37. Scheduler / Calendar

- [ ] Scheduler panel with Day/Week/Month view navigation
- [ ] Period label updates on view change (e.g. "24 Mar - 30 Mar 2026")
- [ ] Resource dropdown filters by technician
- [ ] New Appointment creates with current time, saves to DB
- [ ] Delete with confirmation
- [ ] appointments + appointment_resources tables
- [ ] Links to tasks (task_id) and SD tickets (ticket_id)

## 38. Authentication Framework (built)

- [ ] LoginWindow shows SSO buttons when identity_providers configured in DB
- [ ] Email-based IdP discovery: enter email, system routes to correct provider
- [ ] "Sign in with Microsoft" button opens system browser, OIDC+PKCE flow
- [ ] "Sign in with Okta" button opens system browser, OIDC+PKCE flow
- [ ] "Sign in with SSO" button triggers SAML2 SP-initiated flow
- [ ] Duo MFA prompt appears after SAML if duo_enabled=true in config
- [ ] Brute-force lockout: 5 failed password attempts locks for 30 minutes
- [ ] Claims mapping: external group claim maps to Central role via claim_mappings table
- [ ] JIT provisioning: first-time external user created in app_users automatically
- [ ] Session refresh timer: silently re-validates every 20 minutes
- [ ] Auth state indicator in status bar: Online (green), Entra/Okta/SSO (blue), Offline (amber)
- [ ] Auth Events panel shows all login/logout/failed events with timestamps
- [ ] Identity Providers panel: CRUD for providers + domain mappings
- [ ] Existing Windows auto-login and manual password login still work unchanged

## 39. Grid Customizer & Saved Filters

- [ ] Right-click any column header shows "Customize Grid..." menu item
- [ ] GridCustomizerDialog: row height, alternating rows, summary footer, group panel, auto-filter
- [ ] Settings persist per-user per-panel across restarts (panel_customizations table)
- [ ] Right-click shows "Manage Saved Filters..." menu item
- [ ] SavedFilterDialog: save current filter with name, load, delete, set default
- [ ] Default saved filter auto-applies on panel load
- [ ] Quick filter presets appear in column right-click menu (up to 10)
- [ ] "Clear All Filters" in right-click menu
- [ ] Customizer wired to all 22 grids across all modules

## 40. Panel Floating / Multi-Monitor

- [ ] Any panel tab can be dragged out to a separate window
- [ ] Floating panels are real OS windows (not child windows) — FloatingMode.Desktop
- [ ] Floating panels can be moved to second monitor and maximized
- [ ] Drag floating panel back to dock it into the main window
- [ ] Right-click tab → "Float" works as alternative to dragging
- [ ] EnableGlobalFloating covers all panels including closed ones
- [ ] Layout save/restore preserves floating panel positions

## 41. Command-Line Startup Args

- [ ] `--dsn "Host=..."` overrides database connection string
- [ ] `--auth-method offline` starts in offline mode without login dialog
- [ ] `--auth-method password --user admin --password secret` auto-logs in
- [ ] Password cleared from memory after use (ClearPassword)

## 42. MainWindow Partial Class Extraction

- [ ] MainWindow.AdminPanels.cs contains all admin panel loading methods
- [ ] MainWindow.xaml.cs reduced from 6815 to 6459 lines
- [ ] Build succeeds with 0 errors after extraction

## 43. Enterprise Mediator + Link Engine

- [ ] Mediator singleton handles all in-process panel messaging with pipeline behaviors
- [ ] MediatorLoggingBehavior logs all messages to debug output
- [ ] MediatorPerformanceBehavior tracks per-message-type counts and avg latency
- [ ] Mediator.GetDiagnostics() returns subscription and message count stats
- [ ] Filtered subscriptions: handlers only called when filter function returns true
- [ ] PanelMessageBus.Publish bridges to Mediator automatically (backward compatible)
- [ ] LinkEngine manages DB-stored LinkRules (source panel/field → target panel/field)
- [ ] Default link rules: SD Technicians→Requests, Requesters→Requests, Groups→Requests, Devices→Switches, Users→AuthEvents
- [ ] Right-click grid → "Configure Links..." opens LinkCustomizerDialog
- [ ] LinkCustomizerDialog shows source/target panel dropdowns + field names + active toggle
- [ ] Link rules persisted in panel_customizations table (setting_type='link')
- [ ] Link rules loaded from DB on panel open and added to LinkEngine
- [ ] Cross-panel filtering works: click tech in SD Technicians → Request grid auto-filters

## 44. API Endpoints (new)

- [ ] /api/identity/providers — CRUD for identity providers
- [ ] /api/identity/domain-mappings — email domain → provider routing
- [ ] /api/identity/claim-mappings — claims → role mapping rules
- [ ] /api/identity/auth-events — read-only auth audit trail
- [ ] /api/appointments — CRUD with date range filter + resources
- [ ] /api/locations/countries — CRUD
- [ ] /api/locations/regions — CRUD with country filter
- [ ] /api/locations/references — reference config list
- [ ] /api/locations/references/next/{type} — atomic next reference number
- [ ] /api/backup/run — trigger pg_dump backup
- [ ] /api/backup/history — backup history list
- [ ] /api/backup/tables — list all DB tables
- [ ] /api/backup/migrations — migration history
- [ ] /api/backup/purge-counts — soft-deleted record counts
- [ ] /api/backup/purge/{table} — purge soft-deleted records

## 45. Real-Time Notifications (new tables)

- [ ] pg_notify triggers on all new tables (migration 048)
- [ ] SignalR DataChanged handler covers: identity_providers, appointments, countries, regions, reference_config, backup_history, icon_defaults, sd_technicians, sd_groups, sd_teams
- [ ] Panel loaded flags reset on DataChanged → panel refreshes on next open
- [ ] Toast notifications shown for multi-user changes

## 46. Grid Context Menu (global)

- [ ] Right-click column header shows: Customize Grid, Manage Saved Filters, Configure Links, Clear All Filters
- [ ] Right-click row cell shows same menu items
- [ ] Menu items appear on all 22 grids across all modules
- [ ] Separator before custom items for clean visual separation

## 47. Integration Sync Engine

- [ ] Sync Config panel opens from Admin > System > Sync Engine
- [ ] Sync configs grid shows Name, AgentType, Enabled, Direction, Interval, Status
- [ ] Add Config creates new row with defaults
- [ ] Run Sync executes SyncEngine.ExecuteSyncAsync for selected config
- [ ] Test Connection checks agent availability
- [ ] Sync log grid (bottom) loads on config selection, shows history
- [ ] ManageEngine agent registered (agent_type='manage_engine')
- [ ] 7 field converters registered: direct, constant, combine, split, lookup, date_format, expression
- [ ] Entity maps define source entity → target table mapping per config
- [ ] Field maps define source field → target column + converter per entity map
- [ ] Concurrent sync throttled by SemaphoreSlim (max_concurrent setting)
- [ ] Sync status updated on sync_configs (last_sync_at, last_sync_status, last_error)
- [ ] Sync log appended to sync_log table per execution
- [ ] Cancel sync via SyncEngine.CancelSync(configId)

## 48. Sync Engine API

- [ ] GET /api/sync/configs — list all sync configurations
- [ ] PUT /api/sync/configs — create/update sync config
- [ ] GET /api/sync/configs/{id}/entity-maps — entity maps for config
- [ ] GET /api/sync/configs/{id}/log — sync execution log
- [ ] POST /api/sync/configs/{id}/run — trigger sync execution
- [ ] GET /api/sync/agent-types — list registered agent types
- [ ] GET /api/sync/converter-types — list registered converter types

## 49. Unit Tests (126 total)

- [ ] 47 existing SD model tests (SdRequest, SdFilterState, dirty tracking, PanelMessageBus)
- [ ] 11 Mediator tests (publish, subscribe, filter, unsubscribe, diagnostics, pipeline, async)
- [ ] 8 LinkEngine tests (initialize, register/unregister, add/remove rules, clear)
- [ ] 24 Auth framework tests (AuthResult, UserTypes, AuthStates, SecureString, IdentityProviderConfig, ClaimMapping, AppUser)
- [ ] 15 Admin model tests (ReferenceConfig, ContainerInfo, MigrationRecord, BackupRecord, GridSettings, Location, Appointment)
- [ ] 21 Integration tests (7 converter types, SyncEngine agent registration, execute sync, disabled entity, converter pipeline)

## 50. Sync Engine Agents

- [ ] ManageEngine agent (agent_type='manage_engine') — OAuth refresh, paged read, write-back
- [ ] CSV Import agent (agent_type='csv_import') — reads CSV/TSV files with header detection
- [ ] CSV agent supports quoted fields, configurable delimiter and encoding
- [ ] CSV agent GetFieldsAsync returns column names from header row
- [ ] REST API agent (agent_type='rest_api') — connects to any JSON REST endpoint
- [ ] REST agent supports auth: none, bearer, basic, api_key
- [ ] REST agent supports pagination: offset, page, cursor
- [ ] REST agent configurable data path (e.g. "data", "results.items")
- [ ] REST agent flattens nested JSON objects (extracts "name" or "id" from sub-objects)
- [ ] All 3 agents registered at startup in SyncEngine.Instance

## 51. CSV Export (Global)

- [ ] Right-click any grid row → "Export to CSV..." opens SaveFileDialog
- [ ] CSV exported via DX TableView.ExportToCsv()
- [ ] Default filename includes panel name + date (e.g. Devices_20260328.csv)
- [ ] "Copy to Clipboard" copies all visible rows
- [ ] Toast notification on successful export or error
- [ ] Available on all 22 grids via GridCustomizerHelper

## 52. Webhook Receiver

- [ ] POST /api/webhooks/{source} receives JSON payload without auth
- [ ] Payload stored in webhook_log table with source, headers, payload
- [ ] Invalid JSON wrapped in {"raw": "..."} object
- [ ] SignalR "WebhookReceived" event broadcast to all connected clients
- [ ] Auto-marks matching sync_config as 'pending' for next sync cycle
- [ ] GET /api/webhooks — list recent webhooks
- [ ] GET /api/webhooks/{id}/payload — retrieve full payload
- [ ] Migration 050 creates webhook_log table with pg_notify trigger

## 53. Home Dashboard

- [ ] Dashboard panel is the first tab (before Devices)
- [ ] Platform KPIs: Total Devices, Active Switches, Active Users, Total Links, VLANs, Tasks Open
- [ ] Service Desk KPIs: Open Tickets, Closed Today, Avg Resolution (hours), SLA Compliant (%)
- [ ] System Health KPIs: Sync Configs, Sync Failures, Auth Events (24h), Failed Logins (24h)
- [ ] All KPI cards show trend arrows (green/red) vs previous period
- [ ] Recent Activity feed shows last 20 auth events + sync log entries
- [ ] Refresh button reloads all dashboard data
- [ ] Last refreshed timestamp shown
- [ ] Dashboard loads on panel activation (lazy)

## 54. Email Notification Service

- [ ] EmailService.Instance configurable with SMTP settings
- [ ] Configure() accepts host, port, username, password, from address, SSL
- [ ] SendAsync sends text or HTML emails to one or multiple recipients
- [ ] Predefined templates: sync failure alert, auth lockout alert, backup complete
- [ ] SendTestEmailAsync for testing SMTP configuration
- [ ] Non-blocking — email failures don't crash the app

## 55. Cron Expression Parser

- [ ] CronExpression.Parse("0 */6 * * *") — every 6 hours
- [ ] CronExpression.Parse("30 2 * * *") — daily at 02:30
- [ ] CronExpression.Parse("*/15 * * * *") — every 15 minutes
- [ ] CronExpression.Parse("0 9-17 * * 1-5") — hourly weekdays 9-5
- [ ] Matches(DateTime) returns true when time matches expression
- [ ] GetNextOccurrence(after) returns next matching time
- [ ] TryParse returns false for invalid expressions
- [ ] Supports: *, ranges (1-5), steps (*/15), lists (1,3,5)

## 56. Cron Integration in Job Scheduler

- [ ] job_schedules table has schedule_cron column (migration 051)
- [ ] Jobs with cron expression run when current minute matches cron
- [ ] Jobs without cron use interval_minutes (backward compatible)
- [ ] next_run_at calculated from CronExpression.GetNextOccurrence
- [ ] Cron-based jobs filtered at check time — no extra DB polling
- [ ] Both cron and interval jobs visible in Jobs admin panel

## 57. Dashboard API

- [ ] GET /api/dashboard — returns full DashboardData (platform, SD, health, activity)
- [ ] All counts query from live DB tables
- [ ] Activity feed combines auth_events + sync_log (last 20)

## 58. Unit Tests (146 total)

- [ ] 47 SD model tests
- [ ] 11 Mediator tests
- [ ] 8 LinkEngine tests
- [ ] 24 Auth framework tests
- [ ] 15 Admin model tests
- [ ] 21 Integration/sync engine tests
- [ ] 14 Cron expression tests (parse, match, next occurrence, ranges, steps, lists, weekdays)
- [ ] 6 Email service tests (configure, not configured, dictionary config, send without SMTP)

## 59. TOTP MFA

- [ ] TotpService.GenerateSecret() returns valid Base32 secret
- [ ] GenerateQrUri produces otpauth:// URI for authenticator apps
- [ ] GenerateCurrentCode returns 6-digit code matching the secret
- [ ] VerifyCode validates current TOTP within ±1 time step (clock skew)
- [ ] VerifyCode rejects wrong codes
- [ ] GenerateRecoveryCodes returns 8 unique hyphenated hex codes
- [ ] Recovery codes stored hashed in mfa_recovery_codes table
- [ ] VerifyRecoveryCodeAsync marks code as used (single-use)
- [ ] EnableMfaAsync sets mfa_enabled=true and stores encrypted secret
- [ ] DisableMfaAsync clears secret and recovery codes

## 60. Password Policy

- [ ] Default policy: min 8, uppercase, lowercase, digit, special, 90-day expiry
- [ ] Relaxed policy: min 4, no complexity, no expiry (for dev/test)
- [ ] Validate rejects: too short, no uppercase, no lowercase, no digit, no special
- [ ] Validate accepts strong passwords that meet all requirements
- [ ] Password history: blocks reuse of last N passwords
- [ ] IsExpired: returns true when password_changed_at > ExpiryDays ago
- [ ] IsTooRecent: blocks change if password changed < MinAgeDays ago
- [ ] Multiple validation errors all reported in one result
- [ ] Description property shows human-readable policy summary
- [ ] Migration 052 creates password_history table

## 61. Structured Audit Trail

- [ ] AuditService.Instance logs all CRUD operations
- [ ] LogCreateAsync records entity creation with after snapshot
- [ ] LogUpdateAsync records before/after snapshots as JSONB
- [ ] LogDeleteAsync records entity deletion
- [ ] LogViewAsync records data access
- [ ] LogExportAsync records data exports
- [ ] LogLoginAsync records login attempts
- [ ] LogSettingChangeAsync records old/new setting values
- [ ] audit_log table with before_json/after_json columns (migration 052)
- [ ] GetAuditLogAsync supports filtering by entity_type and username
- [ ] SetPersistFunc wires to DbRepository at startup
- [ ] Audit logging never blocks the primary operation (try/catch)

## 62. Unit Tests (171 total)

- [ ] 47 SD model tests
- [ ] 11 Mediator + 8 LinkEngine tests
- [ ] 24 Auth framework + 7 TOTP tests + 18 Password policy tests
- [ ] 15 Admin model + 21 Integration tests
- [ ] 14 Cron + 6 Email tests

## 63. MFA Enrollment Dialog

- [ ] MfaEnrollmentDialog shows TOTP secret key formatted with spaces for readability
- [ ] QR URI displayed + copy button for authenticator app enrollment
- [ ] 6-digit verification code input with Enter key support
- [ ] Correct code shows "Verified! MFA is now enabled" in green
- [ ] Wrong code shows error in red
- [ ] Recovery codes (8) generated and displayed after successful verification
- [ ] Copy Recovery Codes button copies to clipboard
- [ ] OnMfaEnabled delegate called with secret + recovery codes on success

## 64. Password Policy in SetPasswordWindow

- [ ] Policy description shown below user label (e.g. "Min 8 chars, uppercase, digit...")
- [ ] Password validated against PasswordPolicy.Default before save
- [ ] Validation errors shown in red (all errors at once)
- [ ] Password history checked against last 5 hashes in password_history table
- [ ] password_changed_at updated on app_users after password change
- [ ] New hash saved to password_history for future reuse prevention
- [ ] Audit log entry created on password change

## 65. Audit Trail Wiring

- [ ] AuditService initialized at startup with DbRepository persistence
- [ ] Device save logs Create/Update with device name
- [ ] Device delete logs Delete with device ID and name
- [ ] User delete logs Delete with user ID and username
- [ ] CSV export logs Export with panel name and file path
- [ ] Password change logs PasswordChange with user ID
- [ ] GET /api/audit returns audit entries (filterable by entityType, username)

## 66. Health Check API

- [ ] GET /api/health — returns { status: "healthy", timestamp, uptime } (no auth)
- [ ] GET /api/health/detailed — DB latency, table counts, system info, sync engine, mediator diagnostics
- [ ] DB check returns latency_ms and healthy/unhealthy
- [ ] System info includes version, runtime, OS, memory, uptime, processor count
- [ ] Sync engine shows registered agent_types and converter_types
- [ ] Mediator diagnostics show subscription and message counts

## 67. API Rate Limiting

- [ ] Rate limiter middleware: 200 requests per 60 seconds per IP
- [ ] Returns 429 Too Many Requests when exceeded with Retry-After header
- [ ] X-RateLimit-Limit and X-RateLimit-Remaining headers on every response
- [ ] Health checks, webhooks, and SignalR hubs excluded from rate limiting
- [ ] Stale window cleanup when map exceeds 10,000 entries

## 68. API Key Authentication

- [ ] X-API-Key header checked by middleware before JWT
- [ ] Key validated against api_keys table (SHA256 hash — raw key never stored)
- [ ] Valid key sets ClaimsPrincipal with name, role, auth_method=api_key
- [ ] last_used_at and use_count updated on each use
- [ ] Falls through to JWT auth if no X-API-Key header
- [ ] Migration 053 creates api_keys table with pg_notify

## 69. MFA Enrollment in Ribbon

- [ ] "Setup MFA" button in Admin > User Management ribbon group
- [ ] Opens MfaEnrollmentDialog for current authenticated user
- [ ] On successful verification: encrypts secret, saves to app_users.mfa_secret_enc
- [ ] Recovery codes saved hashed to mfa_recovery_codes table
- [ ] Audit log entry created on MFA enable
- [ ] Button uses ProtectedAction_16x16 icon

## 70. API Key Management Panel

- [ ] API Keys panel opens from Admin > Identity > API Keys
- [ ] Generate Key button prompts for name, creates key, shows raw key once
- [ ] Raw key copied to clipboard automatically
- [ ] Grid shows name, role, active, uses, last used, created, expires
- [ ] Revoke button sets is_active=false without deleting
- [ ] Delete button removes key permanently with confirmation
- [ ] Audit log entry on key create and revoke

## 71. Audit Log Viewer Panel

- [ ] Audit Log panel opens from Admin > Identity > Audit Log
- [ ] Grid shows timestamp, action, entity type, entity ID/name, user, details, before/after JSON
- [ ] Entity type dropdown filter (Device, Switch, User, Setting)
- [ ] Username text filter
- [ ] Refresh button reloads with current filters
- [ ] Filter change triggers auto-refresh
- [ ] Sorted by timestamp descending (newest first)
- [ ] Before/After JSON columns for change comparison

## 72. Notification Preferences

- [ ] notification_preferences table stores per-user event/channel pairs (migration 054)
- [ ] 8 event types: sync_failure, sync_complete, auth_lockout, backup_complete/failure, data_changed, password_expiry, webhook_received
- [ ] Channels: toast, email, both, none
- [ ] UpsertNotificationPreferenceAsync for save
- [ ] GetNotificationPreferencesAsync loads user's preferences
- [ ] EventDescription property provides human-readable labels

## 73. Active Sessions Panel

- [ ] Sessions panel opens from Admin > Identity > Sessions
- [ ] Grid shows user, name, auth method, machine, IP, started, last activity, duration
- [ ] Force Logout button terminates selected session
- [ ] Force Logout All terminates all sessions for the selected user
- [ ] CreateSessionAsync called on login, EndSessionAsync on logout
- [ ] ForceEndSessionAsync/ForceEndAllSessionsAsync for admin use
- [ ] active_sessions table with pg_notify trigger (migration 054)
- [ ] Audit log entries for force logout actions

## 74. Session Tracking in Auth Flow

- [ ] CreateSessionAsync called in EstablishSessionAsync after successful login
- [ ] Session token (GUID) generated and stored in active_sessions table
- [ ] Machine name recorded from Environment.MachineName
- [ ] EndSessionAsync called in LogoutAsync on logout
- [ ] Session token cleared after logout

## 75. Notification Preferences Panel

- [ ] My Notifications panel opens from Admin > Identity > My Notifications
- [ ] Grid shows all 8 event types with channel dropdown (toast/email/both/none) and enabled toggle
- [ ] Missing preferences auto-filled with defaults (toast, enabled)
- [ ] Save All saves all preferences at once
- [ ] Auto-save on cell change
- [ ] Preferences cached in NotificationService.LoadPreferences on panel open

## 76. Event-Aware Notification Service

- [ ] NotifyEvent checks user preferences before showing notification
- [ ] Channel "none" suppresses toast
- [ ] Channel "email" triggers EmailRequested event (wired to EmailService)
- [ ] Channel "both" shows toast + triggers email
- [ ] Channel "toast" shows toast only (default)
- [ ] Events without preferences default to toast

## 77. Email + Notification Wiring

- [ ] EmailService configured from app settings (smtp_host, port, username, password, from)
- [ ] NotificationService.EmailRequested wired to EmailService at startup
- [ ] Email sent to current user's email address when channel is "email" or "both"
- [ ] Notification prefs loaded from DB at startup after auth

## 78. Notification API

- [ ] GET /api/notifications/preferences — current user's notification prefs
- [ ] PUT /api/notifications/preferences — update event type channel/enabled
- [ ] GET /api/notifications/sessions — all active sessions (admin)
- [ ] DELETE /api/notifications/sessions/{id} — force end session

## 79. Unit Tests (179 total)

- [ ] 8 notification service tests: default toast, suppress none, toast channel, email channel, both channels, disabled suppression, preference reload, recent cap 50
- [ ] All previous test suites passing (mediator, link, auth, TOTP, password, cron, email, sync, models)

## 80. Email + Security Settings in Backstage

- [ ] Email settings group in backstage: smtp_host, port, username, password, from, SSL
- [ ] Security settings group: password min length, lockout threshold/duration, expiry days, require MFA
- [ ] Settings persisted via SettingsProvider (per-user DB-backed)

## 81. Event Notifications Wired Globally

- [ ] Auth lockout triggers NotifyEvent("auth_lockout") with username and attempt count
- [ ] Sync failure triggers NotifyEvent("sync_failure") with config name and error
- [ ] Sync completion triggers NotifyEvent("sync_complete") with read/created/failed counts
- [ ] Backup completion triggers NotifyEvent("backup_complete") with file size and path
- [ ] All events routed through user's notification preferences (toast/email/both/none)

## 82. New User Notification Pref Seeding

- [ ] JIT-provisioned users get default notification prefs for all 8 event types
- [ ] Default channel: toast, enabled: true

## 83. API Key Management API

- [ ] GET /api/keys — list all API keys (name, role, active, uses, last used)
- [ ] POST /api/keys/generate — create key, returns raw key once
- [ ] POST /api/keys/{id}/revoke — soft-disable key
- [ ] DELETE /api/keys/{id} — hard-delete key

## 84. Saved Filters Migration Fix

- [ ] Migration 055 creates saved_filters table (was referenced but never created)
- [ ] Indexes on user_id + panel_name
- [ ] pg_notify trigger on saved_filters

## 85. Import Wizard

- [ ] Ctrl+I opens Import Wizard dialog
- [ ] Browse button opens file picker (CSV/TSV)
- [ ] Target table dropdown lists all DB tables
- [ ] Upsert key field (default: id)
- [ ] Field mapping grid: CSV Column, Sample Value, Target Column, Converter, Skip checkbox
- [ ] Column names auto-suggested from CSV headers (snake_case)
- [ ] Sample values shown from first row
- [ ] 6 converter types available: direct, constant, expression, date_format, combine, split
- [ ] Import button processes all rows with field mapping + converters
- [ ] Progress shown: imported count + failed count
- [ ] Audit log entry on successful import
- [ ] Notification toast on completion

## 86. Keyboard Shortcuts (complete list)

- [ ] Ctrl+R / F5 — Refresh all data
- [ ] Ctrl+N — New record (routes by active panel)
- [ ] Delete — Delete selected record
- [ ] Ctrl+E — Export devices
- [ ] Ctrl+P — Print preview
- [ ] Ctrl+F — Toggle global search
- [ ] Ctrl+S — Save/commit current row
- [ ] Ctrl+D — Toggle details panel
- [ ] Ctrl+G — Go to dialog
- [ ] Ctrl+I — Import wizard
- [ ] Ctrl+Z — Undo
- [ ] Ctrl+Y — Redo
- [ ] Ctrl+Tab — Cycle to next panel
- [ ] F1 — Keyboard help

## 87. Import Button in Ribbon

- [ ] "Import Data" button in Home > Export ribbon group
- [ ] Uses Import_16x16 icon
- [ ] Opens ImportWizardDialog (same as Ctrl+I)

## 88. Import API

- [ ] POST /api/import — accepts { target_table, upsert_key, records[] }
- [ ] Each record is a JSON object with field:value pairs
- [ ] Returns { imported, failed, target_table }
- [ ] GET /api/import/tables — list available target tables
- [ ] Table name validated against pg_tables whitelist (SQL injection safe)

## 89. Global Grid Right-Click Menu (expanded)

- [ ] Print Preview available on all 22 grids via right-click
- [ ] Column Chooser available on all grids via right-click
- [ ] Best Fit All Columns available on all grids via right-click
- [ ] Select All Rows available on all grids via right-click
- [ ] Full menu: Customize Grid, Manage Filters, Configure Links, Export CSV, Copy, Clear Filter, Print, Column Chooser, Best Fit, Select All

## 90. Data Validation Service

- [ ] DataValidationService.Instance singleton with registered entity rules
- [ ] Required rule: rejects null/empty/whitespace
- [ ] MinLength rule: rejects strings shorter than threshold
- [ ] MaxLength rule: rejects strings longer than threshold
- [ ] Regex rule: rejects strings not matching pattern
- [ ] Range rule: rejects values outside min/max bounds
- [ ] Custom rule: accepts Func<object?, bool> validator
- [ ] Multiple rules per entity, all errors reported in one result
- [ ] RegisterDefaults() seeds rules for Device, User, SdRequest, Appointment, Country, ReferenceConfig
- [ ] Registered at startup in App.OnStartup

## 92. Unit Tests (191 total)

- [ ] 12 validation service tests: required, min/max length, regex, custom, multi-rule, defaults, error summary
- [ ] All previous suites passing (179 + 12 = 191)

## 93. Startup Health Check

- [ ] StartupHealthCheck.CheckAsync verifies critical DB tables exist at startup
- [ ] 25 required tables checked (app_users, roles, switches, sd_requests, sync_configs, audit_log, etc.)
- [ ] Missing tables reported in startup.log
- [ ] Warnings for empty app_users table
- [ ] DB latency measured and logged
- [ ] Results logged before auth flow begins

## 94. Validation API

- [ ] POST /api/validation/validate/{entityType} — validates JSON body against registered rules
- [ ] Returns { isValid, errors[], errorSummary }
- [ ] Validation rules registered at API startup via RegisterDefaults()

## 95. Settings Export/Import Service

- [ ] SettingsExportService.ExportAsync — exports user settings, notification prefs, panel customizations, saved filters as JSON
- [ ] ExportToFileAsync — writes JSON to file
- [ ] ImportFromFile — parses exported settings JSON
- [ ] Useful for backup, migration between machines, cloning user config

## 96. Platform Status API

- [ ] GET /api/status — returns complete platform overview (auth required)
- [ ] platform: name, version, runtime, OS, machine, uptime, processors, memory
- [ ] database: status, latency, table count, missing tables, warnings
- [ ] data: devices, switches, users, links, VLANs, tasks, SD tickets
- [ ] auth: identity providers, API keys, auth events 24h, failed logins 24h
- [ ] sync: configs, enabled, failures, agent types, converter types
- [ ] mediator: subscription and message diagnostics

## 97. Settings Export API

- [ ] GET /api/settings/export — exports current user's settings as JSON
- [ ] Includes: user_settings, notification prefs, panel customizations, saved filters
- [ ] Returns application/json content type

## 98. CsvImportAgent Tests

- [ ] TestConnection fails with no path
- [ ] TestConnection fails with missing file
- [ ] Read parses valid CSV with headers
- [ ] Read handles quoted CSV fields with commas
- [ ] Read supports TSV delimiter
- [ ] Read without header uses col_N naming
- [ ] GetEntityNames returns filename stem
- [ ] GetFields returns CSV headers
- [ ] Write returns read-only error

## 99. RestApiAgent Tests

- [ ] AgentType returns "rest_api"
- [ ] TestConnection fails with no base_url
- [ ] TestConnection fails with invalid URL
- [ ] Initialize accepts all config options without error
- [ ] GetEntityNames returns configured endpoint
- [ ] Delete returns not-implemented

## 100. Unit Tests (209 total)

- [ ] 9 CSV agent tests + 6 REST agent tests + 3 settings export tests
- [ ] All 194 previous tests still passing (209 total)

## 101. Global Search API

- [ ] GET /api/search?q=term — searches across devices, switches, users, SD tickets, tasks
- [ ] Returns unified results with EntityType, EntityId, Title, Subtitle, Badge
- [ ] Case-insensitive ILIKE search
- [ ] Minimum 2 character query
- [ ] Configurable limit (default 50, max 200)
- [ ] Graceful handling of missing tables

## 102. Activity Timeline API

- [ ] GET /api/activity/global — combined audit log + auth events feed (admin)
- [ ] GET /api/activity/me — current user's personal activity timeline
- [ ] Results sorted by time descending
- [ ] Includes source (audit/auth), action, entity, username

## 103. SignalR Hub Events (expanded)

- [ ] SendNotification — eventType, title, message, severity
- [ ] SendWebhookReceived — source, webhookId
- [ ] SendAuditEvent — action, entityType, entityName, username
- [ ] SendSyncComplete — configName, status, recordsRead, recordsFailed
- [ ] SendSessionEvent — eventType, username, authMethod
- [ ] All events broadcast to all connected clients

## 104. Audit Broadcasting via SignalR

- [ ] AuditService.SetBroadcastFunc wires SignalR broadcasting
- [ ] Every audit log entry triggers real-time broadcast to all connected clients
- [ ] Broadcast includes: action, entityType, entityName, username
- [ ] Broadcasting never blocks the primary operation (try/catch)

## 105. Unit Tests (225 total)

- [ ] 3 agent registration tests (all agents registered, all converters registered, duplicate overwrites)
- [ ] 7 password hasher tests (generate salt, unique salts, hash consistency, different salts, verify correct/wrong, empty password)
- [ ] 6 panel customization tests (GridSettings defaults, FormLayout defaults, LinkRule defaults, FieldGroup defaults, GridSettings serialization round-trip, LinkRule list serialization round-trip)
- [ ] All 209 previous tests still passing

## 106. Swagger API Documentation

- [ ] All endpoint groups have WithTags for organized Swagger UI
- [ ] Tags: Auth, Devices, Switches, Links, VLANs, BGP, Tasks, Admin, SSH, Jobs, Scheduler, Identity, Notifications, Sync, Platform
- [ ] Swagger description updated for full platform scope
- [ ] Duplicate /api/notifications registration removed
- [ ] Swagger UI at /swagger with security definition for Bearer JWT

## 107. API Client (extended)

- [ ] SearchAsync — global search
- [ ] GetDashboardAsync — platform KPIs
- [ ] GetStatusAsync — complete platform status
- [ ] GetActivityAsync / GetMyActivityAsync — activity feeds
- [ ] GetSyncConfigsAsync / RunSyncAsync — sync engine control
- [ ] GetAuditLogAsync — audit trail with entity filter
- [ ] GetIdentityProvidersAsync — identity provider list
- [ ] ImportAsync — bulk data import
- [ ] HealthCheckAsync — no-auth health check

## 108. AuthContext Tests (12)

- [ ] Initial state: not authenticated, null user, NotAuthenticated state
- [ ] SetSession: sets user, permissions, sites, auth state
- [ ] HasPermission: granted permissions work, ungrantied denied
- [ ] SuperAdmin (priority 1000): always has all permissions
- [ ] Site access: no restrictions = all sites; restricted = only listed
- [ ] SetOfflineAdmin: full permissions, offline state
- [ ] Logout: clears everything
- [ ] UpdateAllowedSites: changes site access live
- [ ] HasAnyPermission: true if any one matches
- [ ] PermissionCount: reflects granted count
- [ ] All 9 AuthStates enum values present

## 109. AuditService Tests (7)

- [ ] No persist func: does not throw
- [ ] Persist func called with correct entry
- [ ] Broadcast func called with action
- [ ] LogCreateAsync sets Create action
- [ ] LogDeleteAsync sets Delete action
- [ ] Before/After JSON serialized correctly
- [ ] Persist throws: does not crash

## 110. Sync Model Tests (9)

- [ ] SyncConfig StatusColor: success/failed/running/partial/never
- [ ] SyncLogEntry StatusColor: success/failed/running
- [ ] SyncEntityMap PropertyChanged fires
- [ ] SyncFieldMap PropertyChanged fires
- [ ] SyncConfig PropertyChanged fires

## 111. Unit Tests (253 total)

- [ ] 12 AuthContext + 7 AuditService + 9 SyncModel tests = 28 new
- [ ] All 225 previous tests still passing (253 total)

## 112. Notification + Model Tests (14)

- [ ] NotificationPreference EventDescription for all 8 known types
- [ ] Unknown event type returns raw string
- [ ] NotificationEventTypes.All has 8 entries
- [ ] ActiveSession Duration formats correctly
- [ ] ActiveSession StatusColor: active=green, inactive=grey
- [ ] ApiKeyRecord PropertyChanged fires
- [ ] DashboardData defaults (zeros, empty activity)
- [ ] ActivityItem defaults (empty strings)
- [ ] SavedFilter IsShared (null UserId = shared)
- [ ] SavedFilter PropertyChanged fires
- [ ] AdUser defaults
- [ ] AdConfig IsConfigured (empty=false, has domain=true)
- [ ] IconOverride defaults

## 113. Cron Edge Case Tests (7)

- [ ] Sunday (day 0) matching
- [ ] Multiple comma values (0,15,30,45)
- [ ] First day of month (day 1)
- [ ] Specific month (January only)
- [ ] GetNextOccurrence skips non-matching months efficiently
- [ ] Step from non-zero start (5/15 = 5,20,35,50)
- [ ] Invalid field count throws FormatException

## 114. Unit Tests (274 total)

- [ ] 14 notification/model + 7 cron edge cases = 21 new
- [ ] All 253 previous tests still passing (274 total)
- [ ] Test suite runs in ~4 seconds

## 115. Sync Pipeline Integration Tests (5)

- [ ] Full pipeline with direct mapping: agent → read → map fields → upsert (2 records)
- [ ] Full pipeline with converters: combine (full name), expression (lowercase), constant (user type)
- [ ] Upsert failure counted as failed record (not crash)
- [ ] Empty source: success with 0 records
- [ ] Multiple entity maps synced concurrently (table_a + table_b)

## 116. Unit Tests (300 total)

- [ ] 5 sync pipeline integration tests
- [ ] 4 location model tests (Country/Region/Postcode PropertyChanged, lat/long)
- [ ] 4 appointment model tests (defaults, PropertyChanged for Appointment + Resource)
- [ ] 8 identity config tests (IdentityProviderConfig, ClaimMapping, DomainMapping, ExternalIdentity, AuthEvent, AuthResult claims, AuthRequest)
- [ ] 5 mediator advanced tests (subscriber ID, message count tracking, multiple subscribers, multiple filters, logging behavior)
- [ ] All 279 previous tests still passing (300 total)
- [ ] Test suite runs in ~4 seconds

## 117. Auto-Migration on Startup

- [ ] MigrationRunner checks db/migrations/ for pending .sql files on startup
- [ ] Pending migrations applied automatically before health check
- [ ] Count of applied migrations logged to startup.log
- [ ] Splash shows "Applied N database migrations" when migrations run
- [ ] No error if migrations directory doesn't exist

## 118. K8s Health Probes

- [ ] GET /api/health/ready — checks DB connectivity, returns 503 if unavailable
- [ ] GET /api/health/live — always returns 200 (process alive check)
- [ ] Both endpoints require no authentication

## 119. Startup Banner

- [ ] Splash shows "Central vX.X.X — Initializing..." with assembly version
- [ ] startup.log includes version, machine name, .NET version

## 120. CLI Arg Parsing Tests (5)

- [ ] Empty args returns all nulls
- [ ] --dsn flag parsed correctly
- [ ] Short flags (-s, -u, -p, -a) all parsed
- [ ] Long flags (--server, --auth-method) all parsed
- [ ] Mixed short + long flags in same invocation

## 121. Unit Tests (305 total)

- [ ] 5 CLI parsing tests
- [ ] All 300 previous tests still passing (305 total)

## 122. ContainerInfo Model Tests (3)

- [ ] StateColor for all states (running/exited/paused/created/empty)
- [ ] IsRunning true only for "running" state
- [ ] PropertyChanged fires for Name, State, Image

## 123. BackupRecord Model Tests (2)

- [ ] FileSizeDisplay all ranges (null, bytes, KB, MB, GB)
- [ ] StatusColor for all statuses (success/running/failed/unknown)

## 124. UserTypes + AppUser Tests (8)

- [ ] UserTypes.All has 5 entries
- [ ] IsProtected: System and Service only
- [ ] IsProtected: Standard, ActiveDirectory, Admin, null, empty = false
- [ ] AppUser.Initials: two-word name → first letters
- [ ] AppUser.Initials: single word → first 2 chars
- [ ] AppUser.Initials: from username when DisplayName empty
- [ ] AppUser.StatusText: Active/Inactive
- [ ] AppUser.StatusColor: green/grey

## 125. Complete Test Suite Summary (318 total)

- [ ] **Auth tests (49)**: AuthContext 12, AuthFramework 24, PasswordHasher 7, TOTP 7, PasswordPolicy 18, IdentityConfig 8, UserTypes 8
- [ ] **Shell tests (22)**: Mediator 11+5, LinkEngine 8, PanelMessageBus 5+, PanelCustomization 6
- [ ] **Model tests (50)**: SD models 47+, Admin models 15, Location 4, Appointment 4, Container 3, Backup 2, Notification 14
- [ ] **Integration tests (40)**: SyncEngine 5+5+3, FieldConverters 15+7, CsvAgent 9, RestAgent 6, SyncModels 9, SyncPipeline 5
- [ ] **Service tests (39)**: CronExpression 14+7, Email 6, Notification 8, Audit 7, Validation 12, Settings 3, StartupArgs 5

## 126. Engine Services (existing, now documented)

- [ ] NotificationService.Instance — singleton toast system with Info/Success/Warning/Error
- [ ] NotificationService.Recent — last 50 notifications
- [ ] NotificationService.NotificationReceived event for shell rendering
- [ ] UndoService.Instance — undo/redo stack with RecordAdd/RecordRemove/RecordEdit
- [ ] PanelMessageBus — static pub/sub with 4 message types bridged to Mediator
- [ ] IconService — singleton icon metadata cache, admin/user resolution chain
- [ ] IconOverrideService — 2-layer resolution (user → admin → code fallback)
- [ ] SvgHelper — SVG→WPF ImageSource via Svg.NET, currentColor→white, disk + memory cache
- [ ] LayoutService — save/restore grids, dock, window bounds, preferences per user
- [ ] ConnectivityManager — DB connection with offline mode, 5s timeout, 10s retry

## 127. DX Offline Package Cache

- [ ] 78 DevExpress 25.2.5 NuGet packages downloaded to packages-offline/
- [ ] NuGet.config references DevExpress-Offline as local source
- [ ] Includes: all WPF controls, 17 themes, 23 dependency packages
- [ ] Total size: ~194 MB
- [ ] Enables fully offline development/build

## 128. Grid Context Menu (complete list per grid)

- [ ] Customize Grid... → opens GridCustomizerDialog
- [ ] Manage Saved Filters... → opens SavedFilterDialog
- [ ] Configure Links... → opens LinkCustomizerDialog
- [ ] Export to CSV... → SaveFileDialog + TableView.ExportToCsv
- [ ] Copy to Clipboard → SelectAll + CopyToClipboard
- [ ] Clear All Filters → resets grid.FilterString
- [ ] Print Preview... → TableView.ShowPrintPreview
- [ ] Column Chooser... → TableView.ShowColumnChooser
- [ ] Best Fit All Columns → TableView.BestFitColumns
- [ ] Select All Rows → grid.SelectAll
- [ ] Quick filter presets (up to 10 saved filters inline)
- [ ] Separator between groups for visual clarity

## 129. Cross-Session Platform State

- [ ] All settings persisted in user_settings table (per-user, key-value)
- [ ] Grid layouts saved/restored via LayoutService
- [ ] Dock layout (panel positions, floating) saved/restored
- [ ] Window bounds (position, size, maximized) saved/restored
- [ ] Theme preference saved/restored
- [ ] Panel open states saved/restored
- [ ] Saved filters per panel per user
- [ ] Grid customizations per panel per user (JSONB)
- [ ] Link rules per panel per user
- [ ] Notification preferences per user per event type

## 130. Deployment Infrastructure

- [ ] Dockerfile includes Api.Client project reference
- [ ] Dockerfile copies db/migrations/ for auto-apply
- [ ] HEALTHCHECK directive pings /api/health every 30s
- [ ] pod.yaml has postgres + api containers with resource limits
- [ ] pod.yaml uses PG 18 alpine with performance tuning
- [ ] API container env: DSN, JWT secret, credential key
- [ ] db/setup.sh applies all migrations + seed in order
- [ ] db/seed.sql creates admin user (admin/admin), default roles, permissions, lookups

## 131. CI/CD Pipeline

- [ ] GitHub Actions workflow on push to main/develop and PRs
- [ ] Steps: checkout, setup .NET 10, restore, build (x64 Release), test
- [ ] Test results published via dotnet-trx reporter
- [ ] Container build job on main branch only (after tests pass)
- [ ] Podman build + tag + health test in CI

## 132. First-Time Setup

- [ ] Start pod: `podman play kube infra/pod.yaml`
- [ ] Apply migrations: `./db/setup.sh` or auto-apply on app startup
- [ ] Default admin: admin/admin (System user, cannot be deleted)
- [ ] Default roles: Admin (100), Operator (50), Viewer (10)
- [ ] Default lookups: status, device_type, building
- [ ] Default notification preferences seeded for admin user

## 133. PowerShell Setup Script

- [ ] db\setup.ps1 applies all migrations via psql
- [ ] Parses DSN parameter into PGHOST/PGPORT/PGUSER/PGPASSWORD env vars
- [ ] Runs seed.sql after migrations
- [ ] Colour-coded output (cyan/yellow/green/red)
- [ ] Default DSN for localhost development

## 134. Converter Edge Case Tests (11)

- [ ] Direct: null, int, bool pass through
- [ ] Constant: always returns expression regardless of input
- [ ] Combine: empty expression → empty string
- [ ] Split: null value, invalid expression handled
- [ ] DateFormat: null and invalid string handled
- [ ] Expression: empty value with $value ref
- [ ] Lookup: null value handled

## 135. Unit Tests (329 total)

- [ ] 11 converter edge case tests
- [ ] All 318 previous tests still passing

## 136. Security Headers Middleware

- [ ] X-Frame-Options: DENY (prevents clickjacking)
- [ ] X-Content-Type-Options: nosniff (prevents MIME sniffing)
- [ ] X-XSS-Protection: 1; mode=block
- [ ] Referrer-Policy: strict-origin-when-cross-origin
- [ ] Content-Security-Policy: default-src 'none'; frame-ancestors 'none'
- [ ] Permissions-Policy: camera=(), microphone=(), geolocation=()
- [ ] Cache-Control: no-store, no-cache, must-revalidate (default for API)

## 137. Request Logging Middleware

- [ ] All requests logged: method, path, status code, duration, user
- [ ] Slow requests (>1000ms) logged at Warning level with [SLOW] tag
- [ ] Error responses (4xx/5xx) logged at Warning level
- [ ] Normal requests logged at Information level
- [ ] Anonymous requests show "anonymous" as user

## 138. Developer README

- [ ] desktop/README.md with quick start (5 steps)
- [ ] Solution structure table (14 projects)
- [ ] Key features summary
- [ ] Default login credentials
- [ ] Environment variables documentation
- [ ] Migration and API documentation links

## 139. API Middleware Stack (complete, ordered)

- [ ] SecurityHeaders → RequestLogging → RateLimit → ApiKeyAuth → Authentication → Authorization
- [ ] 4 middleware files in Central.Api/Middleware/
- [ ] All registered in Program.cs in correct order

## 140. Version API

- [ ] GET /api/version — returns product name, version, build date, runtime, OS, architecture
- [ ] Lists all available endpoint paths
- [ ] No auth required (desktop client compatibility check)

## 141. Platform Integration Tests (11)

- [ ] All 6 singletons exist (Mediator, SyncEngine, LinkEngine, AuditService, DataValidationService, NotificationService)
- [ ] Key permission codes defined (devices:read, admin:users, admin:ad, admin:backup, scheduler:read)
- [ ] AuthStates enum has 9 values
- [ ] NotificationEventTypes has 8 events with expected values
- [ ] PasswordPolicy.Default is reasonable (min 8, uppercase, digit, 90d expiry)
- [ ] PasswordPolicy.Relaxed is permissive (min 4, no complexity, no expiry)

## 142. Final Test Suite (340 total)

- [ ] 11 platform integration tests
- [ ] All 329 previous tests still passing (340 total)
- [ ] Full suite runs in ~4 seconds

## 143. Enterprise V2 — Phase 0: Multi-Tenancy Foundation

- [ ] Central.Tenancy project added to solution (15th project)
- [ ] ITenantContext interface: TenantId, TenantSlug, SchemaName, Tier, IsResolved
- [ ] TenantContext.Default for backward-compatible single-tenant mode
- [ ] ITenantConnectionFactory: OpenConnectionAsync sets search_path to tenant schema
- [ ] OpenPlatformConnectionAsync sets search_path to central_platform
- [ ] Schema name validated (alphanumeric + underscore only, prevents SQL injection)
- [ ] TenantSchemaManager: ProvisionTenantAsync creates schema + applies all migrations
- [ ] TenantSchemaManager: DropTenantSchemaAsync (refuses to drop public/central_platform)
- [ ] TenantSchemaManager: ListTenantSchemasAsync returns all tenant_* schemas
- [ ] TenantSchemaManager: EnsurePlatformSchemaAsync creates central_platform schema
- [ ] Migration 056 creates central_platform schema with all cross-tenant tables
- [ ] Tables: tenants, subscription_plans, tenant_subscriptions, module_catalog, tenant_module_licenses
- [ ] Tables: license_keys, global_users, tenant_memberships, environments
- [ ] Tables: release_channels, client_versions, tenant_version_policy, client_installations
- [ ] Seed: 3 subscription plans (Free/Professional/Enterprise), 8 module catalog entries, 3 release channels
- [ ] Default tenant seeded (id=00000000..., slug=default, tier=enterprise) for backward compatibility
- [ ] Tenant models: Tenant, SubscriptionPlan, TenantSubscription, ModuleLicense, GlobalUser, TenantMembership, EnvironmentProfile, ClientVersion
- [ ] API project references Central.Tenancy
- [ ] All 340 existing tests still pass (no breaking changes)

## 144. Enterprise V2 — Phase 1: Registration + Subscription + Licensing

- [ ] Central.Licensing project added to solution (16th project)
- [ ] RegistrationService: RegisterAsync creates global user + tenant + free subscription + base module licenses
- [ ] Email verification via verify token
- [ ] Slug generation from company name (URL-safe)
- [ ] Slug uniqueness check with fallback suffix
- [ ] Tenant schema provisioned after registration (all migrations replayed)
- [ ] SubscriptionService: GetSubscriptionAsync, CheckLimitsAsync, UpgradeAsync, GetPlansAsync
- [ ] Limit enforcement: max_users and max_devices per plan
- [ ] Subscription expiry check
- [ ] Plan upgrade: cancel current + create new + update tenant tier
- [ ] ModuleLicenseService: IsModuleLicensedAsync, GetModulesAsync, GrantModuleAsync, RevokeModuleAsync
- [ ] Module catalog: 8 modules (4 base + 4 premium)
- [ ] LicenseKeyService: RSA-4096 signed license keys
- [ ] License payload: tenant_id, hardware_id, modules[], expires_at, issued_at
- [ ] Offline validation: public key embedded in client, verify signature + hardware + expiry
- [ ] License revocation server-side
- [ ] API: POST /api/register/register — self-service registration
- [ ] API: POST /api/register/verify-email — email verification
- [ ] API: GET /api/register/check-slug/{slug} — slug availability
- [ ] API: GET /api/register/subscription/plans — list plans
- [ ] API: GET /api/register/modules — module license status
- [ ] API: POST /api/register/modules/{code}/activate — activate module
- [ ] API: POST /api/register/license/issue — issue signed license key
- [ ] All 340 tests still pass, 0 build errors

## 145. Enterprise V2 — Phase 2: Multi-Tenancy Enforcement

- [ ] TenantResolutionMiddleware extracts tenant_slug from JWT claims
- [ ] Falls back to X-Tenant header for API key auth
- [ ] Defaults to "default" tenant for backward compatibility
- [ ] Skips resolution for public endpoints (health, version, register, webhooks)
- [ ] Sets TenantContext.SchemaName to "tenant_{slug}" or "public" for default
- [ ] TenantContext registered as scoped service in DI
- [ ] ModuleLicenseMiddleware maps API paths to module codes
- [ ] Returns 403 with module requirement when license missing
- [ ] Enterprise tier bypasses module license checks
- [ ] Unauthenticated requests skip module check
- [ ] 10 API path prefixes mapped to 7 module codes
- [ ] NotificationHub: OnConnectedAsync joins tenant group
- [ ] NotificationHub: OnDisconnectedAsync leaves tenant group
- [ ] All 8 Send* methods broadcast to tenant group (not Clients.All)
- [ ] GetTenantGroup helper resolves group from JWT tenant_slug claim
- [ ] Middleware pipeline order: SecurityHeaders → Logging → RateLimit → ApiKey → Auth → TenantResolution → ModuleLicense
- [ ] 6 middleware files in Central.Api/Middleware/
- [ ] All 340 existing tests still pass

## 146. Enterprise V2 — Phase 3: Client Binary Protection

- [ ] Central.Protection project added to solution (17th project)
- [ ] HardwareFingerprint: combines CPU ID, disk serial, machine name, MAC address → SHA256
- [ ] Generate() returns deterministic fingerprint per machine
- [ ] GenerateShort() returns first 16 chars for display
- [ ] WMI queries via System.Management for CPU + disk serial
- [ ] ClientLicenseValidator: RSA public key embedded, validates signed license offline
- [ ] Checks: signature integrity, hardware binding, expiry
- [ ] DPAPI-encrypted local cache for 7-day offline grace period
- [ ] IsModuleLicensed(code) checks cached license modules
- [ ] IsInGracePeriod flag for UI indicator
- [ ] CertificatePinningHandler: HttpClientHandler with SHA-256 public key pinning
- [ ] ValidateCertificate compares server cert fingerprint against pinned set
- [ ] TrustAll() factory for development (bypasses pinning)
- [ ] CalculateFingerprint(cert) helper for extracting pins
- [ ] IntegrityChecker: SHA-256 of all Central*.dll files at runtime
- [ ] VerifyAll(manifest) checks against signed manifest
- [ ] GenerateManifest() creates manifest from current DLLs
- [ ] VerifySelf(expectedHash) verifies the executing assembly
- [ ] IntegrityResult: IsIntact, VerifiedFiles, TamperedFiles, MissingFiles
- [ ] All 340 tests still pass

## 147. Enterprise V2 — Phase 4: Auto-Update Manager

- [ ] Central.UpdateClient project added to solution (18th project)
- [ ] UpdateManager: CheckForUpdateAsync queries /api/updates/check with current version + platform
- [ ] Returns null if up-to-date, UpdateInfo if newer version available
- [ ] ApplyUpdateAsync: downloads package with progress callback
- [ ] Verifies SHA-256 checksum before extracting
- [ ] Backs up current Central*.dll + Central.exe before overwrite
- [ ] Extracts ZIP to app directory, overwrites existing files
- [ ] Rollback on extraction failure (restores backup)
- [ ] Reports success/failure to /api/updates/report
- [ ] Rollback() restores backed-up files
- [ ] RestartApplication() launches new Central.exe and exits current process
- [ ] API: GET /api/updates/check — returns update info if newer version exists
- [ ] API: POST /api/updates/publish — publish new version with manifest + package URL
- [ ] API: GET /api/updates/versions — list all published versions
- [ ] API: POST /api/updates/report — client reports update result
- [ ] UpdateInfo: Version, PackageUrl, Checksum, ReleaseNotes, IsMandatory, DeltaFrom
- [ ] UpdateResult: Success/Fail with NewVersion and RequiresRestart
- [ ] All 340 tests still pass

## 148. Enterprise V2 — Phase 5: Environment Routing

- [ ] EnvironmentService singleton manages Live/Test/Dev connection profiles
- [ ] Profiles stored locally in %LocalAppData%/Central/environments.json
- [ ] SwitchTo(name) changes active environment + fires EnvironmentChanged event
- [ ] GetApiUrl() / GetSignalRUrl() / GetCertFingerprint() from active profile
- [ ] 3 default profiles seeded: Local Development, Test, Production
- [ ] AddProfile / RemoveProfile for admin management
- [ ] EnvironmentProfile: Name, Type (dev/test/live), ApiUrl, SignalRUrl, CertFingerprint, TenantSlug
- [ ] TypeColor: green=live, amber=test, blue=dev
- [ ] TypeLabel: LIVE, TEST, DEV

## 149. Enterprise V2 — Phase 6: Concurrent Editing

- [ ] Central.Collaboration project added to solution (19th project)
- [ ] PresenceService: tracks who is editing which records in-memory
- [ ] JoinEditing / LeaveEditing / DisconnectAll per connection
- [ ] GetEditors returns all users editing a specific entity
- [ ] GetTenantPresence returns all editing sessions for a tenant
- [ ] ConflictDetector: compares row_version, identifies conflicting fields
- [ ] Three-way merge: base → client changes + server changes
- [ ] Non-overlapping changes auto-merged, overlapping flagged
- [ ] MergeResult: MergedValues, AutoMergedFields, ConflictingFields, CanAutoMerge
- [ ] FieldConflict: FieldName, ClientValue, ServerValue, BaseValue

## 150. Enterprise V2 — Phase 7: Item-Level Security (ABAC)

- [ ] Central.Security project added to solution (20th project)
- [ ] SecurityPolicyEngine: evaluates ABAC policies against principal + resource
- [ ] CanAccessRow: row-level access control per entity type
- [ ] GetHiddenFields: field-level visibility per user context
- [ ] FilterFields: strips hidden fields from response
- [ ] SecurityPolicy: EntityType, PolicyType (row/field), Effect (allow/deny), Conditions, HiddenFields, Priority
- [ ] SecurityContext: Username, Role, Department, SecurityClearance
- [ ] Conditions support: exact match, NOT match (! prefix)
- [ ] Policies loaded per-tenant, cached in memory

## 151. Enterprise V2 — Phase 8: Observability

- [ ] Central.Observability project added to solution (21st project)
- [ ] CorrelationContext: AsyncLocal<string> for request correlation ID propagation
- [ ] BeginScope creates new correlation scope (disposable)
- [ ] StructuredLogEntry: Timestamp, Level, Message, CorrelationId, TenantSlug, Username, DurationMs
- [ ] ToCef() exports in Common Event Format for SIEM integration
- [ ] Level-to-severity mapping (Critical=10, Error=7, Warning=5, Info=3, Debug=1)

## 152. Enterprise V2 — Complete Solution (21 projects)

- [ ] Central.Core — engine framework
- [ ] Central.Data — PostgreSQL repos
- [ ] Central.Api — REST + SignalR API
- [ ] Central.Api.Client — typed HTTP client
- [ ] Central.Desktop — WPF shell
- [ ] Central.Module.Devices / Switches / Links / Routing / VLANs / Admin / Tasks / ServiceDesk — 8 modules
- [ ] Central.Tests — 340 unit tests
- [ ] Central.Tenancy — multi-tenant schema isolation
- [ ] Central.Licensing — registration, subscriptions, module licensing, RSA keys
- [ ] Central.Protection — hardware fingerprint, client license validator, cert pinning, integrity checker
- [ ] Central.UpdateClient — auto-update manager with rollback
- [ ] Central.Collaboration — presence tracking, conflict detection, three-way merge
- [ ] Central.Security — ABAC policy engine, row/field-level security
- [ ] Central.Observability — correlation IDs, structured logging, CEF export

## 153. Enterprise V2 Tests (30 new, 370 total)

- [ ] 9 tenancy tests: default context, custom tenant, schema validation, models (Tenant, Plan, GlobalUser, Environment, ClientVersion, ModuleLicense)
- [ ] 11 collaboration tests: presence join/leave/disconnect/multi-editor/tenant-isolation, conflict detection (no conflict, version mismatch), three-way merge (non-overlapping, overlapping, both-same-value)
- [ ] 5 security tests: no policies=allow, deny policy blocks, field hiding, field filtering, priority ordering
- [ ] 5 observability tests: correlation ID generation, set/get, scope restore, CEF format, severity mapping

## 154. CommandGuard (Global Re-Entrancy Protection)

- [ ] CommandGuard.TryEnter/Exit prevents concurrent execution of same command
- [ ] RunAsync wraps async actions with automatic TryEnter/Exit
- [ ] Run wraps sync actions with automatic TryEnter/Exit
- [ ] IsRunning tracks current state
- [ ] Different command names are independent (cmd_a doesn't block cmd_b)
- [ ] Applied to: GlobalAdd, GlobalDelete, AddTask, AddSubTask
- [ ] Tasks: save to DB before adding to collection (gets real ID, prevents tree key=0 hang)

## 155. Tasks Module Fix

- [ ] Tasks tab appears as permanent top-level ribbon tab (not context tab)
- [ ] Permission gate removed from AddPage (buttons still permission-checked)
- [ ] tasks:read/write/delete permission codes added to P constants
- [ ] Migration 057 seeds permissions + grants to Admin/Operator/Viewer
- [ ] Panels group with Tasks check button added
- [ ] Rapid add: CommandGuard prevents concurrent inserts
- [ ] Each new task saved to DB immediately (gets unique ID before tree insert)

## 156. Unit Tests (377 total)

- [ ] 7 CommandGuard tests: enter/exit, re-entrancy block, IsRunning, sync/async Run, independent commands

## 157. CommandGuard Applied Globally

- [ ] GlobalAdd_ItemClick wrapped with CommandGuard.Run("GlobalAdd")
- [ ] GlobalDelete_ItemClick wrapped with CommandGuard.TryEnter/Exit("GlobalDelete")
- [ ] RefreshButton_ItemClick wrapped with CommandGuard.RunAsync("Refresh")
- [ ] GlobalSaveLayout_ItemClick wrapped with CommandGuard.RunAsync("SaveLayout")
- [ ] All fire-and-forget panel loads (`_ = Load*Async()`) are intentional and safe
- [ ] Duplicate device/link handlers are safe (GridControl handles Id=0 better than TreeListControl)

## 158. SafeAsync — Crash-Proof Async Handler Wrapper

- [ ] SafeAsync.Run wraps async void handlers with try/catch
- [ ] Exceptions routed to NotificationService.Error instead of crashing app
- [ ] Context string included in error messages for debugging
- [ ] SafeAsync.RunGuarded combines CommandGuard + safe exception handling
- [ ] RunGuarded releases guard even on exception (finally block)
- [ ] 28 async void handlers identified without try/catch — SafeAsync pattern available for all
- [ ] 4 SafeAsync tests: success completion, exception doesn't crash, re-entrancy prevention, guard release on exception

## 159. Unit Tests (381 total)

- [ ] 4 SafeAsync tests
- [ ] All 377 previous tests still passing

## 160. Exception Logging to App Log + Audit Trail

- [ ] TaskScheduler.UnobservedTaskException: logged to AppLogger (DB) + crash.log + AuditService + toast notification
- [ ] DispatcherUnhandledException: logged to AppLogger + crash.log + AuditService + toast + MessageBox for fatal
- [ ] XAML parse errors: recovered gracefully with args.Handled = true + toast
- [ ] Layout overflow: recovered with args.Handled = true
- [ ] All unhandled exceptions appear in: startup.log, crash.log, app_log table, audit_log table, toast notification
- [ ] Admin can see all errors in App Log panel + Audit Log panel

## 161. Sync Engine Resilience (from IntegrationServer port)

- [ ] Migration 058: sync_failed_records (dead letter queue), sync_record_hashes (change detection)
- [ ] SyncRetry.WithRetryAsync: exponential backoff (1s, 2s, 4s), configurable max retries, 30s cap
- [ ] SyncHashDetector: SHA-256 content hash, sorted field keys, skip unchanged records
- [ ] SyncFieldValidator: pre-write validation of required fields and key fields
- [ ] Dead letter queue: failed records stored with error message, retry count, next retry time
- [ ] FailedSyncRecord model: status (pending/retrying/abandoned/resolved), max retries
- [ ] Per-record retry in SyncEngine: 2 retries with 500ms backoff before dead letter
- [ ] Hash lookup callback: skip records that haven't changed since last sync
- [ ] Hash update callback: store new hash after successful write
- [ ] Failed record callback: route to dead letter queue with full source record JSON
- [ ] Validation errors routed to dead letter queue (not retried — data issue, not transient)
- [ ] RecordsSkipped counter for unchanged records
- [ ] All 381 tests still pass

### 26. File Management Service

- [ ] Migration 059: file_store table (UUID PK, filename, description, mime_type, entity attachment, tags, soft delete)
- [ ] Migration 059: file_versions table (FK to file_store, version_number, bytea data, storage_path, MD5 hash)
- [ ] Migration 059: Indexes on entity_type+entity_id, uploaded_by, filename
- [ ] Migration 059: pg_notify triggers on file_store and file_versions
- [ ] FileManagementService singleton: Configure() sets filesystem storage path
- [ ] ComputeMd5: byte array and Stream overloads, hex string output
- [ ] ShouldStoreInline: files <= 10MB stored in DB (bytea), larger on filesystem
- [ ] GetStoragePath: sharded directory structure (first 2 chars of GUID), auto-create
- [ ] SaveToFilesystemAsync / ReadFromFilesystemAsync / DeleteFromFilesystem
- [ ] FileRecord model: Id, Filename, Description, MimeType, FileSize, EntityType, EntityId, UploadedBy, Md5Hash, Tags, VersionCount
- [ ] FileRecord.FileSizeDisplay: human-readable (B, KB, MB, GB)
- [ ] FileVersionRecord model: Id, FileId, VersionNumber, FileSize, Md5Hash, StoragePath, UploadedBy
- [ ] API: POST /api/files/upload (multipart, MD5 verification, inline vs filesystem routing)
- [ ] API: GET /api/files/{id}/download (latest version, correct Content-Type + Content-Disposition)
- [ ] API: GET /api/files/{id}/versions (version history)
- [ ] API: GET /api/files?entity_type=X&entity_id=Y (list files for entity)
- [ ] API: DELETE /api/files/{id} (soft delete)
- [ ] App.xaml.cs: FileManagementService configured with %LocalAppData%/Central/file_storage on startup

## 162. Tasks Module Phase 1 — Hierarchy & Schema Expansion

### Migration 060 — New Tables & Schema
- [ ] Migration 060_tasks_v2.sql applies without errors
- [ ] `portfolios` table created (id, name, description, owner_id, archived)
- [ ] `programmes` table created (id, portfolio_id, name, description, owner_id)
- [ ] `task_projects` table created (id, programme_id, name, scheduling_method, default_mode, method_template, calendar, archived)
- [ ] `project_members` table created (project_id + user_id unique constraint)
- [ ] `sprints` table created (id, project_id, name, start_date, end_date, goal, status, velocity)
- [ ] `releases` table created (id, project_id, name, target_date, description, status)
- [ ] `task_links` table created (source_id, target_id, link_type unique constraint)
- [ ] `task_dependencies` table created (predecessor_id, successor_id unique constraint, dep_type FS/FF/SF/SS)
- [ ] `task_releases` junction table created (task_id, release_id PK)
- [ ] 23 new columns added to `tasks` table (project_id, sprint_id, wbs, is_epic, is_user_story, points, work_remaining, start_date, finish_date, is_milestone, risk, confidence, severity, bug_priority, backlog_priority, sprint_priority, committed_to, category, board_column, board_lane, time_spent, color, hyperlink)
- [ ] pg_notify triggers on all 7 new tables
- [ ] Default project seeded ("Default Project")
- [ ] Existing tasks auto-linked to default project
- [ ] 6 new permissions seeded (projects:read/write/delete, sprints:read/write/delete)
- [ ] Admin role gets all 6 permissions
- [ ] Operator role gets read+write
- [ ] Viewer role gets read only

### Models
- [ ] Portfolio model with INotifyPropertyChanged
- [ ] Programme model with INotifyPropertyChanged
- [ ] TaskProject model with INotifyPropertyChanged + DisplayName computed
- [ ] ProjectMember model with INotifyPropertyChanged
- [ ] Sprint model with INotifyPropertyChanged + DateRange + DisplayName computed
- [ ] Release model with INotifyPropertyChanged
- [ ] TaskLink model with INotifyPropertyChanged + LinkDisplay computed
- [ ] TaskDependency model with INotifyPropertyChanged + DepDisplay computed
- [ ] TaskItem expanded: ProjectId, ProjectName, SprintId, SprintName, Wbs, IsEpic, IsUserStory, Points, WorkRemaining, StartDate, FinishDate, IsMilestone, Risk, Confidence, Severity, BugPriority, BacklogPriority, SprintPriority, CommittedTo, Category, BoardColumn, BoardLane, TimeSpent, Color
- [ ] TaskItem computed: RiskColor, SeverityColor, StartDateDisplay, FinishDateDisplay, PointsDisplay
- [ ] Milestone added to TypeIcon switch

### Repository (DbRepository.Tasks.cs)
- [ ] GetPortfoliosAsync returns all portfolios with owner name
- [ ] UpsertPortfolioAsync inserts (Id=0) and updates
- [ ] DeletePortfolioAsync removes by id
- [ ] GetProgrammesAsync returns all programmes with owner name
- [ ] UpsertProgrammeAsync inserts and updates
- [ ] DeleteProgrammeAsync removes by id
- [ ] GetTaskProjectsAsync returns all projects
- [ ] UpsertTaskProjectAsync inserts and updates
- [ ] DeleteTaskProjectAsync removes by id
- [ ] GetProjectMembersAsync returns members for a project with user names
- [ ] UpsertProjectMemberAsync inserts or updates role (ON CONFLICT)
- [ ] RemoveProjectMemberAsync deletes by project+user
- [ ] GetSprintsAsync returns sprints (optionally filtered by project)
- [ ] UpsertSprintAsync inserts and updates (including velocity on close)
- [ ] DeleteSprintAsync removes by id
- [ ] GetReleasesAsync returns releases (optionally filtered by project)
- [ ] UpsertReleaseAsync inserts and updates
- [ ] DeleteReleaseAsync removes by id
- [ ] GetTaskLinksAsync returns both directions (source and target) for a task
- [ ] UpsertTaskLinkAsync inserts or updates (ON CONFLICT)
- [ ] DeleteTaskLinkAsync removes by id
- [ ] GetTaskDependenciesAsync returns dependencies with predecessor/successor titles
- [ ] UpsertTaskDependencyAsync inserts or updates (ON CONFLICT)
- [ ] DeleteTaskDependencyAsync removes by id
- [ ] GetTasksAsync(projectId) filters by project when provided
- [ ] GetTasksAsync joins task_projects, sprints (current + committed) for names
- [ ] UpsertTaskAsync persists all 23 new fields on insert and update

### API Endpoints (ProjectEndpoints.cs)
- [ ] GET /api/projects returns all task projects
- [ ] POST /api/projects creates new project
- [ ] DELETE /api/projects/{id} deletes project
- [ ] GET /api/projects/portfolios returns all portfolios
- [ ] POST /api/projects/portfolios creates portfolio
- [ ] PUT /api/projects/portfolios/{id} updates portfolio
- [ ] DELETE /api/projects/portfolios/{id} deletes portfolio
- [ ] GET /api/projects/programmes returns all programmes
- [ ] POST /api/projects/programmes creates programme
- [ ] DELETE /api/projects/programmes/{id} deletes programme
- [ ] GET /api/projects/{id}/sprints returns sprints for project
- [ ] POST /api/projects/{id}/sprints creates sprint
- [ ] DELETE /api/projects/{id}/sprints/{sprintId} deletes sprint
- [ ] GET /api/projects/{id}/releases returns releases for project
- [ ] POST /api/projects/{id}/releases creates release

### TaskTreePanel UI (all DX controls)
- [ ] Project selector dropdown (DX ComboBoxEdit) in toolbar — shows all projects + "(All Projects)"
- [ ] Sprint selector dropdown updates when project changes
- [ ] Type filter dropdown (Epic/Story/Task/Bug/SubTask/Milestone)
- [ ] Selecting a project reloads tasks filtered by project
- [ ] Selecting "(All Projects)" shows all tasks
- [ ] WBS column (read-only)
- [ ] Points column with DX SpinEdit (0-999, float)
- [ ] WorkRemaining column with DX SpinEdit (0-9999, float, n1 mask)
- [ ] Sprint name column (read-only)
- [ ] Category dropdown (Feature/Enhancement/TechDebt/Bug/Ops)
- [ ] Risk dropdown (None/Low/Medium/High/Critical)
- [ ] Start Date column with DX DateEdit (yyyy-MM-dd)
- [ ] Finish Date column with DX DateEdit (yyyy-MM-dd)
- [ ] Due Date column with DX DateEdit (yyyy-MM-dd)
- [ ] Color stripe column (4px colored bar from task.color)
- [ ] Milestone added to TaskType dropdown
- [ ] TotalSummary: task count, sum of points, sum of remaining hours
- [ ] ShowFilterPanelMode="ShowAlways" enabled
- [ ] ShowAutoFilterRow enabled
- [ ] AllowColumnFiltering enabled
- [ ] UseEvenRowBackground enabled
- [ ] SearchPanelNullText="Search tasks..." enabled
- [ ] Title column fixed left
- [ ] New tasks inherit selected project ID
- [ ] New sub-tasks inherit parent's project ID

### MainViewModel
- [ ] TaskProjects ObservableCollection added
- [ ] Sprints ObservableCollection added
- [ ] LoadTaskProjectsAsync loads all projects
- [ ] LoadSprintsAsync(projectId) loads sprints filtered by project
- [ ] LoadTasksAsync(projectId) filters tasks by project
- [ ] Projects load on first Tasks panel open

### Permission Codes
- [ ] P.ProjectsRead / P.ProjectsWrite / P.ProjectsDelete constants
- [ ] P.SprintsRead / P.SprintsWrite / P.SprintsDelete constants

## 163. Tasks Phase 2 — Product Backlog & Sprint Planning

### Migration 061 — Sprint Planning Tables
- [ ] Migration 061_sprint_planning.sql applies without errors
- [ ] `sprint_allocations` table created (sprint_id + user_id unique, capacity_hours, capacity_points)
- [ ] `sprint_burndown` table created (sprint_id + snapshot_date unique, points/hours remaining/completed)
- [ ] pg_notify triggers on both tables
- [ ] `snapshot_sprint_burndown()` PG function created and callable

### Models
- [ ] SprintAllocation model with INotifyPropertyChanged (Id, SprintId, UserId, UserName, CapacityHours, CapacityPoints)
- [ ] SprintBurndownPoint model (Id, SprintId, SnapshotDate, PointsRemaining, HoursRemaining, PointsCompleted, HoursCompleted, IdealPoints, IdealHours)

### Repository Methods
- [ ] GetSprintAllocationsAsync(sprintId) returns per-user capacity with names
- [ ] UpsertSprintAllocationAsync inserts or updates (ON CONFLICT)
- [ ] GetSprintBurndownAsync(sprintId) returns daily snapshots ordered by date
- [ ] SnapshotSprintBurndownAsync(sprintId) calls PG function
- [ ] CommitToSprintAsync(taskId, sprintId) sets committed_to on task
- [ ] UncommitFromSprintAsync(taskId) clears committed_to
- [ ] UpdateBacklogPriorityAsync(taskId, priority) sets backlog_priority
- [ ] UpdateSprintPriorityAsync(taskId, priority) sets sprint_priority
- [ ] GetSprintStatsAsync(sprintId) returns total/done points/hours + item count
- [ ] CloseSprintAsync snapshots burndown, records velocity, optionally carries forward incomplete items

### TaskBacklogPanel (DX TreeListControl)
- [ ] Panel opens from ribbon Backlog toggle
- [ ] Project selector dropdown filters backlog items
- [ ] Sprint selector dropdown for commit target
- [ ] "Commit Selected" button sets committed_to on multi-selected items
- [ ] "Uncommit" button clears committed_to on multi-selected items
- [ ] Committed items show sprint name in Sprint column
- [ ] Category filter dropdown (Feature/Enhancement/TechDebt/Bug/Ops)
- [ ] TreeListControl with drag-and-drop enabled (AllowDragDrop)
- [ ] BacklogPriority column sorted ascending (drag-sort order)
- [ ] Inline editing: Title, Category, Points, Tags
- [ ] TotalSummary: item count + sum of points
- [ ] Multi-select rows (MultiSelectMode="Row")
- [ ] Auto-filter row + filter panel + search panel
- [ ] Even row background alternation
- [ ] ValidateNode auto-saves on row commit
- [ ] Panel state saves/restores on restart

### SprintPlanPanel (DX GridControl)
- [ ] Panel opens from ribbon Sprint Plan toggle
- [ ] Project selector loads sprints for project
- [ ] Sprint selector filters grid to sprint items
- [ ] Sprint header shows name, date range, goal
- [ ] Capacity progress bar (blue < 80%, amber 80-100%, red > 100%)
- [ ] Capacity text shows "X / Y pts"
- [ ] Item count displayed
- [ ] "+ New Sprint" creates sprint with 2-week default, reloads selector
- [ ] "Close Sprint" records velocity, snapshots burndown, sets status=Closed
- [ ] GridControl with inline editing (Status, Points, WorkRemaining)
- [ ] SprintPriority column sorted ascending
- [ ] TotalSummary: item count + sum points + sum remaining hours
- [ ] Multi-select rows
- [ ] Auto-filter row + filter panel + search
- [ ] ValidateRow auto-saves on row commit
- [ ] Panel state saves/restores on restart

### SprintBurndownPanel (DX ChartControl)
- [ ] Panel opens from ribbon Burndown toggle
- [ ] Sprint selector loads available sprints
- [ ] Actual burndown line (blue, with markers)
- [ ] Ideal burndown line (grey, thin)
- [ ] X-axis: dates (day scale)
- [ ] Y-axis: remaining (points or hours)
- [ ] Metric toggle: Points / Hours
- [ ] "Snapshot Now" button captures current burndown data point
- [ ] Crosshair shows argument + value labels on hover
- [ ] Legend (top-right): Actual / Ideal
- [ ] Velocity summary bar (bottom): velocity, completed, remaining
- [ ] Chart updates when switching sprints
- [ ] Chart updates when switching metric (Points/Hours)

### MainViewModel
- [ ] IsBacklogPanelOpen property with OnPropertyChanged
- [ ] IsSprintPlanPanelOpen property with OnPropertyChanged
- [ ] IsBurndownPanelOpen property with OnPropertyChanged

### MainWindow Wiring
- [ ] 3 DocumentPanels added (BacklogPanel, SprintPlanningPanel, SprintBurndownDocPanel)
- [ ] 3 BarCheckItem ribbon toggles (Backlog, Sprint Plan, Burndown)
- [ ] DockItemClosing handlers for all 3 panels
- [ ] Startup DockController.Close for all 3 panels
- [ ] Panel state save/restore for backlog, sprintplan, burndown keys
- [ ] Backlog ProjectChanged reloads sprints + tasks
- [ ] Sprint plan ProjectChanged reloads sprints
- [ ] Sprint plan SprintChanged filters grid + updates capacity bar
- [ ] Sprint plan CreateSprint saves + reloads
- [ ] Sprint plan CloseSprint calls CloseSprintAsync + reloads
- [ ] Burndown SprintChanged loads burndown data + renders chart
- [ ] Burndown SnapshotRequested snapshots + reloads chart

## 164. Tasks Phase 3 — Kanban Board

### Migration 062 — Board Configuration Tables
- [ ] Migration 062_kanban_board.sql applies without errors
- [ ] `board_columns` table created (project_id + board_name + column_name unique)
- [ ] `board_lanes` table created
- [ ] pg_notify triggers on both tables
- [ ] Default 5 columns seeded for Default Project (Backlog, To Do, In Progress, Review, Done)
- [ ] WIP limits set on In Progress (5) and Review (3)

### Models
- [ ] BoardColumn model with INotifyPropertyChanged (IsOverWip, WipDisplay, HeaderDisplay computed)
- [ ] BoardLane model with INotifyPropertyChanged
- [ ] KanbanColumnVM view model with ObservableCollection<TaskItem> Cards

### Repository Methods
- [ ] GetBoardColumnsAsync(projectId, boardName) returns columns with live card counts
- [ ] UpsertBoardColumnAsync inserts or updates
- [ ] DeleteBoardColumnAsync removes by id
- [ ] MoveTaskToColumnAsync(taskId, columnName, statusMapping) updates board_column + status
- [ ] GetBoardLanesAsync(projectId, boardName) returns lanes ordered by sort_order

### KanbanBoardPanel UI
- [ ] Panel opens from ribbon Kanban toggle
- [ ] Project selector dropdown loads board for selected project
- [ ] Horizontal scrolling column layout (260px per column)
- [ ] Column headers show name + WIP count (red text when over limit)
- [ ] Cards show: color stripe, title, type badge, priority icon, points, assigned, due date
- [ ] Drag-and-drop cards between columns
- [ ] Drop updates board_column + status (via statusMapping)
- [ ] Card counts update after drag-drop
- [ ] WIP limit indicator turns red when exceeded
- [ ] Swim lane selector (None, Assigned To, Priority, Type)
- [ ] "+ Card" button opens Tasks panel
- [ ] "Board Config" button placeholder for column editor
- [ ] Dark theme styling (dark column backgrounds, card shadows)

### MainWindow Wiring
- [ ] KanbanBoardDocPanel added to DockLayoutManager
- [ ] BarCheckItem Kanban toggle in ribbon
- [ ] IsKanbanPanelOpen ViewModel property
- [ ] DockItemClosing handler
- [ ] Startup DockController.Close
- [ ] Panel state save/restore for "kanban" key
- [ ] ProjectChanged loads columns + tasks + rebuilds board
- [ ] CardMoved persists column + status change

## 165. Elsa Workflows Engine (Central.Workflows)

### Project Structure
- [ ] Central.Workflows project created (net10.0, separate from modules)
- [ ] Elsa 3.5.3 NuGet packages: Elsa, EF Core PostgreSQL, Http, Scheduling, CSharp, Identity, Workflows.Api
- [ ] Project reference to Central.Core (shared models)
- [ ] WorkflowsAssemblyMarker class for assembly scanning
- [ ] Referenced by Central.Api

### WorkflowSetup Registration
- [ ] AddCentralWorkflows(connectionString) extension method on IServiceCollection
- [ ] UseWorkflowManagement with PostgreSQL EF Core persistence
- [ ] UseWorkflowRuntime with PostgreSQL EF Core persistence
- [ ] UseIdentity with admin user provider
- [ ] UseHttp for HTTP trigger activities
- [ ] UseScheduling for timer/cron activities
- [ ] UseCSharp for C# expression support
- [ ] UseWorkflowsApi for REST management endpoints
- [ ] AddActivitiesFrom scans Central.Workflows assembly
- [ ] AddWorkflowsFrom scans Central.Workflows assembly

### Custom Activities (6)
- [ ] UpdateTaskStatusActivity — sets task status, records transition intent (TaskId, NewStatus, TriggeredBy)
- [ ] ValidateTransitionActivity — checks allowed transitions (Open→InProgress, etc.), outputs IsValid + Reason
- [ ] SendNotificationActivity — queues notification (Recipients, Title, Message, Level)
- [ ] ApprovalActivity — suspends workflow with bookmark, resumes on approve/reject (ApproverId, IsApproved, Comment)
- [ ] LogAuditActivity — records audit entry (Action, EntityType, EntityId, PerformedBy, Details)
- [ ] SetFieldActivity — sets entity field value (EntityType, EntityId, FieldName, FieldValue with NOW/NULL support)

### Built-in Workflow
- [ ] TaskStatusTransitionWorkflow — Sequence: ValidateTransition → If valid: UpdateStatus + LogAudit + SendNotification; If invalid: SendWarning

### Migration 063 — Workflow Tracking Tables
- [ ] workflow_assignments table (maps workflow definitions to entity types/scopes)
- [ ] workflow_approvals table (tracks pending/approved/rejected approval requests with bookmark IDs)
- [ ] workflow_execution_log table (Central-side execution log with entity references)
- [ ] pg_notify triggers on assignments + approvals

### API Integration
- [ ] Central.Api references Central.Workflows
- [ ] AddCentralWorkflows(dsn) called in Program.cs
- [ ] app.UseWorkflows() middleware registered
- [ ] Elsa management API available at /elsa/api/* (workflow definitions, instances)
- [ ] Elsa HTTP activities can expose workflow-triggered endpoints
- [ ] Custom activities appear in Elsa activity palette

### Architecture
- [ ] Central.Workflows is a standalone shared module (not tied to Tasks)
- [ ] Any module can consume workflows: Tasks, Devices, ServiceDesk, Admin
- [ ] Activities use workflow variables (SetVariable) for host-side execution
- [ ] ApprovalActivity uses Elsa bookmarks for suspend/resume pattern
- [ ] PostgreSQL used for both Management (definitions) and Runtime (instances) stores

## 166. Tasks Phase 6 — QA & Issue Tracking

### QAPanel (DX GridControl)
- [ ] Panel opens from ribbon "QA / Bugs" toggle
- [ ] Project selector filters bugs by project
- [ ] "+ New Bug" creates task with type=Bug, severity=Major, status=New, category=Bug
- [ ] "Batch Triage" multi-selects bugs → sets severity=Major, bugPriority=Medium, status=Triaged
- [ ] Severity filter dropdown (Blocker/Critical/Major/Minor/Cosmetic)
- [ ] Status filter dropdown (New/Triaged/InProgress/Resolved/Verified/Closed)
- [ ] Bug Priority filter dropdown (Critical/High/Medium/Low)
- [ ] Severity column with colored dot indicator (red=Blocker/Critical, amber=Major, yellow=Minor, grey=Cosmetic)
- [ ] Bug Priority column (editable dropdown, separate from severity)
- [ ] Status column with bug-specific workflow states (New → Triaged → InProgress → Resolved → Verified → Closed)
- [ ] ID column (read-only, fixed left)
- [ ] Title column (editable)
- [ ] Assigned, Reporter columns (read-only)
- [ ] Sprint column (read-only)
- [ ] Points column (SpinEdit)
- [ ] Risk column (editable dropdown)
- [ ] Due Date column (DateEdit)
- [ ] Created column (read-only DateEdit display)
- [ ] Building, Tags columns (editable)
- [ ] TotalSummary: bug count + sum of points
- [ ] Multi-select rows for batch operations
- [ ] Auto-filter row + filter panel + search panel
- [ ] ValidateRow auto-saves on row commit
- [ ] Panel state saves/restores

### QADashboardPanel (DX ChartControl × 4)
- [ ] Panel opens from ribbon "QA Dashboard" toggle
- [ ] Project selector filters all charts
- [ ] "Refresh" button reloads all charts
- [ ] Bugs by Severity chart (bar chart, red, 5 severity levels)
- [ ] Bug Aging chart (bar chart, amber, 6 time buckets: 0-1d, 2-3d, 4-7d, 1-2w, 2-4w, 4w+)
- [ ] Opened vs Closed chart (line chart, red=opened, green=closed, last 30 days)
- [ ] Open Bugs by Assignee chart (bar chart, blue, top 10 assignees)
- [ ] Charts use only open/unresolved bugs (exclude Closed/Verified/Resolved)
- [ ] 2×2 grid layout

### Repository Methods
- [ ] GetBugsAsync(projectId?) returns tasks where task_type='Bug'
- [ ] BatchTriageBugsAsync(ids, severity, bugPriority) bulk updates severity + priority + status

### MainWindow Wiring
- [ ] QADocPanel + QADashboardDocPanel DocumentPanels added
- [ ] 2 BarCheckItem ribbon toggles (QA/Bugs, QA Dashboard)
- [ ] IsQAPanelOpen + IsQADashboardPanelOpen ViewModel properties
- [ ] DockItemClosing, startup Close, state save/restore for qa + qadash keys
- [ ] QA panel: project loads bugs, new bug creates + refreshes, batch triage + refresh
- [ ] QA dashboard: project change + refresh reloads all charts

## 167. Tasks Phase 4 — Gantt Scheduling & Dependencies

### Migration 064 — Baselines
- [ ] task_baselines table created (task_id + baseline_name unique)
- [ ] pg_notify trigger on task_baselines
- [ ] save_project_baseline() PG function (snapshots all project tasks, upserts on conflict)

### Models
- [ ] TaskBaseline model (Id, TaskId, BaselineName, StartDate, FinishDate, Points, Hours, SavedAt)
- [ ] GanttPredecessorLink model (PredecessorTaskId, SuccessorTaskId, LinkType 0-3, Lag)
- [ ] TaskItem.ProgressPercent computed (0-100 from actual/estimated hours, 100 if Done)
- [ ] TaskItem.BaselineStartDate/BaselineFinishDate properties for Gantt overlay

### Repository Methods
- [ ] GetBaselinesAsync(taskId) returns baselines for a task
- [ ] SaveProjectBaselineAsync(projectId, name) calls PG function, returns count
- [ ] GetGanttLinksAsync(projectId?) returns dependencies as GanttPredecessorLink with FS/FF/SS/SF mapping

### GanttPanel (DX GanttControl)
- [ ] Panel opens from ribbon "Gantt" toggle
- [ ] Project selector loads tasks with start/finish dates
- [ ] DX GanttControl with KeyFieldName=Id, ParentFieldName=ParentId
- [ ] StartDateMapping, FinishDateMapping, NameMapping, ProgressMapping bound
- [ ] BaselineStartDateMapping, BaselineFinishDateMapping for overlay
- [ ] Columns: ID, Title (fixed left), Start, Finish, Status, Assigned, Points
- [ ] Milestones: tasks with IsMilestone=true get StartDate=FinishDate (diamond rendering)
- [ ] Tasks without dates get fallback dates from CreatedAt/DueDate
- [ ] Zoom In / Zoom Out / Fit All buttons
- [ ] "Today" button fits range around current date
- [ ] "Save Baseline" captures project baseline (all tasks with dates)
- [ ] "Show Baseline" checkbox toggles baseline overlay
- [ ] "Critical Path" checkbox (placeholder for future row styling)
- [ ] Auto-expand all nodes
- [ ] Inline editing (start/finish dates, title, points)
- [ ] Panel state saves/restores

### MainWindow Wiring
- [ ] GanttDocPanel DocumentPanel added
- [ ] BarCheckItem Gantt toggle with GanttView icon
- [ ] IsGanttPanelOpen ViewModel property
- [ ] DockItemClosing, startup Close, state save/restore for "gantt" key
- [ ] ProjectChanged loads tasks (filtered to those with dates) + gantt links
- [ ] SaveBaselineRequested calls SaveProjectBaselineAsync

## 168. Tasks Phase 7 — Custom Columns & Field Permissions

### Migration 065
- [ ] custom_columns table (project_id + name unique, column_type, config JSONB, default_value, is_required)
- [ ] custom_column_permissions table (column_id, user_id or group_name, can_view, can_edit)
- [ ] task_custom_values table (task_id + column_id PK, value_text, value_number, value_date, value_json)
- [ ] pg_notify triggers on custom_columns + task_custom_values

### Models
- [ ] CustomColumn with ColumnType enum (Text/RichText/Number/Hours/DropList/Date/DateTime/People/Computed)
- [ ] CustomColumn.GetDropListOptions() parses options from config JSON
- [ ] CustomColumn.GetAggregationType() parses aggregation from config (Sum/Avg/Min/Max)
- [ ] CustomColumnPermission (view/edit per user or group)
- [ ] TaskCustomValue with DisplayValue computed by column type
- [ ] TaskItem.CustomValues dictionary for runtime custom data

### Repository Methods
- [ ] GetCustomColumnsAsync(projectId) returns columns ordered by sort_order
- [ ] UpsertCustomColumnAsync inserts or updates (including JSONB config)
- [ ] DeleteCustomColumnAsync removes column + cascades values/permissions
- [ ] GetCustomValuesAsync(taskId) returns values with column name/type
- [ ] GetAllCustomValuesAsync(projectId) returns bulk dictionary for grid loading
- [ ] UpsertCustomValueAsync upserts text/number/date/json value (ON CONFLICT)
- [ ] GetCustomColumnPermissionsAsync(columnId) returns permissions
- [ ] UpsertCustomColumnPermissionAsync inserts or updates

### Dynamic Column Rendering (TaskTreePanel)
- [ ] LoadCustomColumns() adds TreeListColumn dynamically per custom column definition
- [ ] Previous custom columns removed before re-adding (tagged with "CustomColumn")
- [ ] Type-aware editors: SpinEdit for Number/Hours, DateEdit for Date/DateTime, ComboBoxEdit for DropList
- [ ] Unbound columns typed correctly (decimal for Number, DateTime for Date, string for Text)
- [ ] SetCustomValues() populates TaskItem.CustomValues dictionary
- [ ] Custom columns load when project changes in toolbar
- [ ] Column widths: 70px for Number/Hours, 120px for others

## 169. Tasks Phase 8 — Reporting & Dashboards

### Migration 066
- [ ] saved_reports table (project_id, name, folder, query_json JSONB, shared_with JSONB)
- [ ] dashboards table (name, layout_json JSONB, template, shared_with JSONB)
- [ ] dashboard_snapshots table (dashboard_id + snapshot_date unique, data_json JSONB)
- [ ] pg_notify triggers on saved_reports + dashboards

### Models
- [ ] SavedReport with QueryJson, Folder, SharedWith, DisplayPath computed
- [ ] ReportFilter (Field, Operator 11 types, Value, Value2, Logic AND/OR)
- [ ] ReportQuery (Columns, Filters, SortField, SortDirection, GroupField, EntityType)
- [ ] Dashboard with LayoutJson, Template, SharedWith
- [ ] DashboardTile (Row/Column/Span, ChartType 7 types, DataSource, fields, color)

### Repository Methods
- [ ] GetSavedReportsAsync(projectId?) returns reports with creator name
- [ ] UpsertSavedReportAsync inserts or updates (JSONB query + sharing)
- [ ] DeleteSavedReportAsync removes by id
- [ ] GetDashboardsAsync returns all dashboards with creator name
- [ ] UpsertDashboardAsync inserts or updates (JSONB layout)
- [ ] DeleteDashboardAsync removes by id

### ReportBuilderPanel (DX GridControl × 2)
- [ ] Panel opens from ribbon "Reports" toggle
- [ ] Report selector dropdown loads saved reports
- [ ] Entity type selector (task, device, switch)
- [ ] Filter builder: add/clear filter conditions via DX GridControl
- [ ] Filter fields dropdown (26 task fields)
- [ ] 11 operators: =, !=, >, <, >=, <=, contains, between, in, isNull, isNotNull
- [ ] Logic column (AND/OR)
- [ ] "Run Query" executes filters against task data, populates results grid
- [ ] Results grid with auto-generated columns from query result DataTable
- [ ] Results grid: auto-filter row, search, filter panel, summary
- [ ] "Save Report" persists query as JSON
- [ ] "Export CSV" triggers export of result DataTable
- [ ] Loading saved report populates filter grid from stored JSON

### TaskDashboardPanel (DX ChartControl × 4)
- [ ] Panel opens from ribbon "Dashboard" toggle
- [ ] Dashboard selector dropdown
- [ ] Project selector filters all charts
- [ ] "Refresh" button reloads all charts
- [ ] Tasks by Status — pie chart (5 statuses, colored slices, labels)
- [ ] Points by Type — bar chart (blue, 6 task types)
- [ ] Tasks Created (30 days) — line chart (green, daily points)
- [ ] Sprint Velocity — bar chart (amber, last 10 closed sprints)
- [ ] 2×2 grid layout
- [ ] Charts load on panel open with all data

### MainWindow Wiring
- [ ] ReportBuilderDocPanel + TaskDashboardDocPanel DocumentPanels
- [ ] 2 BarCheckItem ribbon toggles (Reports, Dashboard)
- [ ] IsReportBuilderPanelOpen + IsTaskDashboardPanelOpen ViewModel properties
- [ ] DockItemClosing, startup Close, state save/restore for reports + taskdash keys
- [ ] Report builder: RunQuery reflection-based filter engine, SaveReport persists, ExportRequested
- [ ] Task dashboard: ProjectChanged reloads charts, LoadAllTasks/LoadAllSprints data providers

## 170. Tasks Phase 9 — Collaboration & Time Tracking

### Migration 067
- [ ] time_entries table (task_id, user_id, entry_date, hours, activity_type, notes)
- [ ] activity_feed table (project_id, task_id, user_id, action, summary, details JSONB)
- [ ] task_views table (project_id, name, view_type, config_json JSONB, shared_with)
- [ ] pg_notify on all 3 tables
- [ ] log_task_activity() trigger auto-logs create/status_change/assign/delete

### TimesheetPanel (DX GridControl)
- [ ] Panel opens from ribbon toggle
- [ ] Week picker loads entries for Mon-Sun
- [ ] Hours column (SpinEdit 0-24), Activity dropdown (5 types), Notes
- [ ] TotalSummary: sum hours + entry count
- [ ] Total hours green display in toolbar
- [ ] ValidateRow auto-saves

### ActivityFeedPanel
- [ ] Panel opens from ribbon toggle
- [ ] Project selector + refresh
- [ ] Card template: action icon, summary, user, time ago
- [ ] Auto-populated by PG trigger

### Repository
- [ ] GetTimeEntriesAsync, UpsertTimeEntryAsync, DeleteTimeEntryAsync
- [ ] GetActivityFeedAsync, GetTaskViewsAsync, UpsertTaskViewAsync

## 171. Tasks Phase 10 — To-Do, Portfolio, Views

### MyTasksPanel (DX GridControl)
- [ ] Shows tasks assigned to current user across all projects
- [ ] Group By: None/Project/Due/Priority/Status
- [ ] Inline editing Status + WorkRemaining
- [ ] Summary: count, points, remaining

### PortfolioPanel (DX TreeListControl)
- [ ] Portfolio → Programme → Project hierarchy with roll-ups
- [ ] Columns: Name, Level, Tasks, Points, Complete%, OpenBugs, ActiveSprints
- [ ] BuildPortfolioTreeAsync aggregates from all data

### All 4 Panels Wired
- [ ] 4 DocumentPanels + 4 ribbon toggles + 4 ViewModel properties
- [ ] DockItemClosing + startup Close + state persistence for all 4

## 173. Tasks Phase 11 — Import/Export

### TaskImportPanel (3-step wizard with DX controls)
- [ ] Panel opens from ribbon "Import" toggle
- [ ] Step 1: Browse file (OpenFileDialog), format auto-detect (.xlsx/.csv/.xml)
- [ ] Step 1: Project selector for import target
- [ ] Step 2: Column mapping grid — source columns auto-detected from file headers
- [ ] Step 2: Target field dropdown (19 task fields + skip)
- [ ] Step 2: Auto-Detect button maps by name similarity
- [ ] Step 2: Sample value column shows first row data
- [ ] Step 2: Mapped count display ("X of Y mapped")
- [ ] Step 3: Preview button parses file → shows data in auto-column results grid
- [ ] Step 3: Import button creates TaskItem from each row via field mapping
- [ ] Step 3: "Update existing" checkbox matches by Title within project
- [ ] Step 3: Progress bar during import
- [ ] Step 3: Import count status display

### TaskFileParser Service
- [ ] ParseFile() routes by extension (.csv, .xml, .xlsx)
- [ ] CSV parser: header row → columns, comma-split data rows
- [ ] MS Project XML parser: XDocument, handles namespace, extracts Task elements
- [ ] MS Project fields: Name, WBS, Start, Finish, Duration, PercentComplete, Priority, Milestone, PredecessorLink
- [ ] Excel placeholder (returns info message — add EPPlus for full support)

### MainWindow Wiring
- [ ] TaskImportDocPanel DocumentPanel
- [ ] BarCheckItem Import toggle
- [ ] IsTaskImportPanelOpen ViewModel property
- [ ] DockItemClosing + startup Close + state persistence for "taskimport" key
- [ ] ParseFile event → TaskFileParser.ParseFile on background thread
- [ ] ImportTasks event → upsert each task, update existing by title match if checked

## 174. Context Menus on All Task Module Panels

### GridContextMenuBuilder Enhancement
- [ ] AttachTree() overload added for TreeListControl (uses TreeListView.ShowGridMenu)
- [ ] AddMenuItems extracted as shared helper (GridMenuEventArgs)
- [ ] Action? nullable support for all menu items

### Task Tree Panel (right-click)
- [ ] New Task (creates via CommandGuard)
- [ ] New Sub-Task (creates under selected parent)
- [ ] Separator
- [ ] Delete Task (removes from collection)
- [ ] Separator
- [ ] Export to Clipboard
- [ ] Separator
- [ ] Refresh

### Backlog Panel (right-click)
- [ ] Commit to Sprint
- [ ] Uncommit
- [ ] Separator
- [ ] Export to Clipboard
- [ ] Separator
- [ ] Refresh

### Sprint Plan Panel (right-click)
- [ ] New Task in Sprint
- [ ] Separator
- [ ] Export to Clipboard
- [ ] Separator
- [ ] Refresh

### QA Panel (right-click)
- [ ] New Bug (creates with Bug defaults via CommandGuard)
- [ ] Batch Triage
- [ ] Separator
- [ ] Export to Clipboard
- [ ] Separator
- [ ] Refresh

### My Tasks Panel (right-click)
- [ ] Go to Task in Tree (opens Tasks panel, selects task)
- [ ] Separator
- [ ] Export to Clipboard
- [ ] Separator
- [ ] Refresh

### Timesheet Panel (right-click)
- [ ] Log Time
- [ ] Delete Entry
- [ ] Separator
- [ ] Export to Clipboard
- [ ] Separator
- [ ] Refresh

### Report Results Grid (right-click)
- [ ] Export to Clipboard
- [ ] Export to CSV

### Portfolio Panel (right-click)
- [ ] Refresh

### ExportTreeToClipboard Helper
- [ ] SelectAll + CopyToClipboard on TreeListControl
- [ ] Status bar confirmation message

## 175. Task Module Engine Compliance (Checklist Audit)

### SignalR DataChanged Handlers (13 new table handlers)
- [ ] "sprints" / "sprint_allocations" / "sprint_burndown" → reload sprints + toast
- [ ] "task_projects" → reload projects + toast
- [ ] "portfolios" / "programmes" / "releases" → toast notification
- [ ] "task_links" / "task_dependencies" → toast notification
- [ ] "board_columns" / "board_lanes" → toast notification
- [ ] "custom_columns" / "task_custom_values" → toast notification
- [ ] "time_entries" → toast notification
- [ ] "saved_reports" / "dashboards" → toast notification
- [ ] "workflow_approvals" → warning toast

### UndoService Integration
- [ ] Task delete records UndoService.RecordRemove (collection, item, index, description)
- [ ] Undo restores deleted task to collection at original index
- [ ] Undo/Redo buttons update after task delete

### DataModifiedMessage
- [ ] Task delete publishes DataModifiedMessage("tasks", "Task", "Delete")
- [ ] Time entry delete publishes DataModifiedMessage("time_entries", "TimeEntry", "Delete")
- [ ] Task save already publishes DataModifiedMessage (existing)

### ActivePanel Enum + GetActiveGrid
- [ ] SprintPlan added to ActivePanel enum
- [ ] QA added to ActivePanel enum
- [ ] MyTasks added to ActivePanel enum
- [ ] Timesheet added to ActivePanel enum
- [ ] GetActiveGrid returns SprintPlanGridPanel.Grid for SprintPlan
- [ ] GetActiveGrid returns QAGridPanel.Grid for QA
- [ ] GetActiveGrid returns MyTasksViewPanel.Grid for MyTasks
- [ ] GetActiveGrid returns TimesheetViewPanel.Grid for Timesheet
- [ ] Panel activation map: SprintPlanningPanel → SprintPlan
- [ ] Panel activation map: QADocPanel → QA
- [ ] Panel activation map: MyTasksDocPanel → MyTasks
- [ ] Panel activation map: TimesheetDocPanel → Timesheet
- [ ] Home tab actions (Print Preview, Column Chooser, Export, Search) work on SprintPlan/QA/MyTasks/Timesheet

## 176. Task Module Polish — Ribbon, Delete, Backstage

### TasksModule Ribbon Registration (full)
- [ ] Tasks ribbon page with SortOrder 60
- [ ] Actions group: Add Task, Add SubTask, Add Bug, Delete (permission-gated)
- [ ] Sprint group: New Sprint, Close Sprint, Snapshot Burndown (sprints:write)
- [ ] Scheduling group: Save Baseline, Zoom to Fit
- [ ] View group: Refresh (publishes RefreshPanelMessage)
- [ ] Panels group: 15 check buttons for all task panels
- [ ] RegisterPanels: 15 panels with correct permission gates and closedByDefault

### DeleteTaskAsync (DB-persisted delete)
- [ ] DbRepository.DeleteTaskAsync(id) executes DELETE FROM tasks
- [ ] MainViewModel.DeleteTaskAsync(task) calls repo + publishes DataModifiedMessage
- [ ] Context menu delete: UndoService.RecordRemove + remove from collection + DB delete
- [ ] Undo restores to collection (DB re-insert would need SaveTaskAsync on undo)

### Backstage Close All
- [ ] All 15 task panels included in close-all button handler
- [ ] Panels close without error when already closed

## 177. Task API Full Coverage + Detail Panel + Delete Confirm

### TaskEndpoints.cs — Full Field Coverage
- [ ] GET /api/tasks returns all 45 fields (Phase 1 expansion) with project/sprint/committed joins
- [ ] GET /api/tasks?project_id=N filters by project
- [ ] POST /api/tasks accepts all Phase 1 fields in request body
- [ ] PUT /api/tasks/{id} updates all Phase 1 fields
- [ ] POST /api/tasks/{id}/commit — commit task to sprint
- [ ] DELETE /api/tasks/{id}/commit — uncommit from sprint
- [ ] GET /api/tasks/{id}/links — get task links (both directions)
- [ ] POST /api/tasks/{id}/links — create task link (ON CONFLICT upsert)
- [ ] GET /api/tasks/{id}/dependencies — get Gantt dependencies with titles
- [ ] GET /api/tasks/{id}/time — time entries for a task
- [ ] GET /api/tasks/{id}/comments — unchanged
- [ ] POST /api/tasks/{id}/comments — unchanged
- [ ] DELETE /api/tasks/{id} — unchanged

### TaskDetailPanel Wiring
- [ ] TaskDetailDocPanel added to DockLayoutManager
- [ ] IsTaskDetailPanelOpen ViewModel property
- [ ] DockItemClosing + startup Close + state persistence for "taskdetail" key
- [ ] Backstage close-all includes task detail
- [ ] Task tree CurrentItemChanged → shows task in detail panel (when detail panel is open)
- [ ] Detail panel shows: status icon, title, type, priority, building, assigned, created by, due date, hours, tags, description, comments

### Kanban Card Double-Click → Detail
- [ ] CardDoubleClicked event added to KanbanBoardPanel
- [ ] Double-click card opens TaskDetailPanel with that task's data
- [ ] Single click still initiates drag-and-drop (unchanged)
- [ ] ClickCount >= 2 check prevents drag on double-click

### Delete Confirmation Dialog
- [ ] ThemedMessageBox.Show with Yes/No + Warning icon
- [ ] Message includes task title + "will delete all sub-tasks" warning
- [ ] Cancel (No) aborts delete — no data change
- [ ] Confirm (Yes) proceeds with UndoService + collection remove + DB delete

---

Last updated: 2026-03-29
