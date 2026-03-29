# Tasks Module — Phased Buildout Plan

Enterprise project & task management modelled on Perforce P4 Plan (Hansoft).
Builds on existing `Central.Module.Tasks` foundation (tree CRUD, comments, 3 permissions).

**Current state:** Basic TreeListControl with parent/child tasks, status/priority/type
enums, assigned_to, due_date, estimated/actual hours, tags, comments table, API CRUD.

**Target state:** Full Hansoft-class project & task management — portfolio hierarchy,
Gantt scheduling, Kanban boards, sprint planning, QA workflows, dashboards, time tracking.

---

## Phase 1 — Hierarchy & Core Schema Expansion

**Goal:** Establish the full hierarchy model (Portfolio → Programme → Project → Sprint →
Epic → Story → Task → Sub-Task) and expand the item attribute schema to match the spec.

### 1.1 Database Migration (060_tasks_v2.sql)

#### New Tables

```
portfolios
  id              serial PK
  name            varchar(256) NOT NULL
  description     text
  owner_id        int → app_users
  created_at      timestamptz DEFAULT now()
  updated_at      timestamptz DEFAULT now()
  archived        boolean DEFAULT false

programmes
  id              serial PK
  portfolio_id    int → portfolios
  name            varchar(256) NOT NULL
  description     text
  owner_id        int → app_users
  created_at      timestamptz DEFAULT now()
  updated_at      timestamptz DEFAULT now()

projects
  id              serial PK
  programme_id    int → programmes (nullable)
  name            varchar(256) NOT NULL UNIQUE
  description     text
  scheduling_method  varchar(32) DEFAULT 'FixedDuration'   -- FixedDuration | FixedWork
  default_mode    varchar(16) DEFAULT 'Agile'              -- Agile | TaskBased
  method_template varchar(16) DEFAULT 'Scrum'              -- Scrum | Kanban | SAFe | Custom
  calendar        varchar(64)                              -- regional calendar key
  archived        boolean DEFAULT false
  created_at      timestamptz DEFAULT now()
  updated_at      timestamptz DEFAULT now()

project_members
  id              serial PK
  project_id      int → projects
  user_id         int → app_users
  role            varchar(32) DEFAULT 'Member'  -- MainManager | Member | QAUser | ReadOnly
  UNIQUE(project_id, user_id)

sprints
  id              serial PK
  project_id      int → projects NOT NULL
  name            varchar(128) NOT NULL
  start_date      date
  end_date        date
  goal            text
  status          varchar(32) DEFAULT 'Planning'  -- Planning | Active | Closed
  velocity_points numeric(8,1)
  velocity_hours  numeric(8,1)
  created_at      timestamptz DEFAULT now()

releases
  id              serial PK
  project_id      int → projects NOT NULL
  name            varchar(128) NOT NULL
  target_date     date
  description     text
  status          varchar(32) DEFAULT 'Planned'  -- Planned | InProgress | Released
  created_at      timestamptz DEFAULT now()
```

#### Alter `tasks` Table

Add columns to existing `tasks` table:

```sql
ALTER TABLE tasks ADD COLUMN project_id       int REFERENCES projects(id);
ALTER TABLE tasks ADD COLUMN sprint_id        int REFERENCES sprints(id);
ALTER TABLE tasks ADD COLUMN wbs              varchar(64);  -- auto-calculated "2.1.3"
ALTER TABLE tasks ADD COLUMN is_epic          boolean DEFAULT false;
ALTER TABLE tasks ADD COLUMN is_user_story    boolean DEFAULT false;
ALTER TABLE tasks ADD COLUMN user_story       text;         -- "As a... I want... so that..."
ALTER TABLE tasks ADD COLUMN detailed_description text;
ALTER TABLE tasks ADD COLUMN color            varchar(16);  -- hex color for cards/Gantt
ALTER TABLE tasks ADD COLUMN hyperlink        text;
ALTER TABLE tasks ADD COLUMN points           numeric(6,1);
ALTER TABLE tasks ADD COLUMN work_remaining   numeric(8,1);
ALTER TABLE tasks ADD COLUMN budgeted_work    numeric(8,1);
ALTER TABLE tasks ADD COLUMN start_date       date;
ALTER TABLE tasks ADD COLUMN finish_date      date;
ALTER TABLE tasks ADD COLUMN is_milestone     boolean DEFAULT false;
ALTER TABLE tasks ADD COLUMN risk             varchar(16);   -- None|Low|Medium|High|Critical
ALTER TABLE tasks ADD COLUMN confidence       varchar(16);   -- Low|Medium|High
ALTER TABLE tasks ADD COLUMN severity         varchar(16);   -- QA: Cosmetic|Minor|Major|Critical|Blocker
ALTER TABLE tasks ADD COLUMN bug_priority     varchar(16);   -- QA priority (separate from severity)
ALTER TABLE tasks ADD COLUMN backlog_priority int DEFAULT 0;
ALTER TABLE tasks ADD COLUMN sprint_priority  int DEFAULT 0;
ALTER TABLE tasks ADD COLUMN committed_to     int REFERENCES sprints(id);
ALTER TABLE tasks ADD COLUMN category         varchar(64);   -- Feature|Enhancement|TechDebt|Bug|Ops
ALTER TABLE tasks ADD COLUMN board_column     varchar(64);
ALTER TABLE tasks ADD COLUMN board_lane       varchar(64);
ALTER TABLE tasks ADD COLUMN time_spent       numeric(8,1) DEFAULT 0;

-- Release tagging (many-to-many)
CREATE TABLE task_releases (
  task_id    int REFERENCES tasks(id) ON DELETE CASCADE,
  release_id int REFERENCES releases(id) ON DELETE CASCADE,
  PRIMARY KEY (task_id, release_id)
);

-- Cross-project item links
CREATE TABLE task_links (
  id          serial PK,
  source_id   int REFERENCES tasks(id) ON DELETE CASCADE,
  target_id   int REFERENCES tasks(id) ON DELETE CASCADE,
  link_type   varchar(32) NOT NULL,  -- relates_to | blocks | blocked_by | duplicates | parent_of
  lag_days    int DEFAULT 0,
  created_at  timestamptz DEFAULT now(),
  UNIQUE(source_id, target_id, link_type)
);

-- Dependency links for Gantt scheduling
CREATE TABLE task_dependencies (
  id              serial PK,
  predecessor_id  int REFERENCES tasks(id) ON DELETE CASCADE,
  successor_id    int REFERENCES tasks(id) ON DELETE CASCADE,
  dep_type        varchar(4) NOT NULL DEFAULT 'FS',  -- FS | FF | SF | SS
  lag_days        int DEFAULT 0,
  UNIQUE(predecessor_id, successor_id)
);
```

#### pg_notify triggers on all new tables

#### Permissions

```sql
-- Add to permissions table
INSERT INTO permissions (code, description) VALUES
  ('projects:read', 'View projects and portfolios'),
  ('projects:write', 'Create and edit projects'),
  ('projects:delete', 'Delete projects'),
  ('sprints:read', 'View sprints'),
  ('sprints:write', 'Create and manage sprints'),
  ('sprints:delete', 'Delete sprints');
```

### 1.2 Models (Central.Core/Models/)

- `Portfolio.cs` — Id, Name, Description, OwnerId, Archived
- `Programme.cs` — Id, PortfolioId, Name, Description, OwnerId
- `Project.cs` — Id, ProgrammeId, Name, Description, SchedulingMethod, DefaultMode, MethodTemplate, Calendar, Archived
- `ProjectMember.cs` — Id, ProjectId, UserId, UserName, Role
- `Sprint.cs` — Id, ProjectId, Name, StartDate, EndDate, Goal, Status, VelocityPoints, VelocityHours
- `Release.cs` — Id, ProjectId, Name, TargetDate, Description, Status
- `TaskLink.cs` — Id, SourceId, TargetId, LinkType, LagDays
- `TaskDependency.cs` — Id, PredecessorId, SuccessorId, DepType, LagDays
- Expand `TaskItem.cs` with all new fields (points, work_remaining, start_date, finish_date, is_milestone, risk, confidence, severity, etc.)

### 1.3 Repository Methods

- `GetPortfoliosAsync()`, `UpsertPortfolioAsync()`, `DeletePortfolioAsync()`
- `GetProgrammesAsync()`, `UpsertProgrammeAsync()`, `DeleteProgrammeAsync()`
- `GetProjectsAsync()`, `UpsertProjectAsync()`, `DeleteProjectAsync()`
- `GetProjectMembersAsync(projectId)`, `UpsertProjectMemberAsync()`, `RemoveProjectMemberAsync()`
- `GetSprintsAsync(projectId)`, `UpsertSprintAsync()`, `DeleteSprintAsync()`
- `GetReleasesAsync(projectId)`, `UpsertReleaseAsync()`, `DeleteReleaseAsync()`
- `GetTaskLinksAsync(taskId)`, `UpsertTaskLinkAsync()`, `DeleteTaskLinkAsync()`
- `GetTaskDependenciesAsync(taskId)`, `UpsertTaskDependencyAsync()`, `DeleteTaskDependencyAsync()`
- Expand `GetTasksAsync()` to join project/sprint names
- `CalculateWbsAsync(projectId)` — auto-generate WBS codes from hierarchy

### 1.4 API Endpoints

- `/api/portfolios` — CRUD
- `/api/programmes` — CRUD
- `/api/projects` — CRUD + members sub-resource
- `/api/projects/{id}/sprints` — CRUD
- `/api/projects/{id}/releases` — CRUD
- `/api/tasks/{id}/links` — CRUD
- `/api/tasks/{id}/dependencies` — CRUD
- Expand `/api/tasks` with new fields in request/response

### 1.5 UI — Project Selector + Expanded Tree

- **Project selector dropdown** in TaskTreePanel toolbar (filters tasks by project)
- **Sprint grouping** — group-by sprint in tree view
- Add new columns to tree: Points, WorkRemaining, StartDate, FinishDate, Risk, Sprint
- **Aggregation** — parent rows auto-sum Points, EstimatedHours, WorkRemaining from children
- WBS column (auto-calculated, read-only)
- Color indicator column (small colored square from task.color)

### 1.6 Deliverables

- [ ] Migration 060_tasks_v2.sql applied
- [ ] All new models with INotifyPropertyChanged
- [ ] Repository CRUD for all new entities
- [ ] API endpoints for portfolios, programmes, projects, sprints, releases, links, dependencies
- [ ] TaskItem expanded with all new fields
- [ ] Project selector in toolbar
- [ ] Aggregation (points, hours) rolling up in tree
- [ ] Build: 0 errors
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 2 — Product Backlog & Sprint Planning

**Goal:** Backlog view with drag-sort priority, sprint commitment workflow, capacity
planning, and velocity tracking.

### 2.1 Product Backlog Panel (TaskBacklogPanel)

New DocumentPanel — hierarchical backlog view per project.

- **Tree grid** with drag-and-drop reordering (sets `backlog_priority`)
- Epics as collapsible groups, stories/tasks nested underneath
- **Commit to Sprint** — right-click or drag item into a sprint container
  - Sets `committed_to` on the task (reference, not copy)
  - Task appears in both backlog (greyed) and sprint view
- **Category filter** — Feature / Enhancement / Tech Debt / Bug / Ops
- **Release filter** — dropdown to show only items tagged to a release
- **Backlog grooming mode** — inline edit points, priority, category
- Archive completed sections (collapse + grey out)

### 2.2 Sprint Planning Panel (SprintPlanPanel)

New DocumentPanel — sprint-centric view.

- **Sprint selector** dropdown (Planning / Active / Closed)
- **Sprint header** — name, dates, goal, capacity bar (allocated vs available)
- **Sprint backlog grid** — items committed to selected sprint
  - Columns: Priority, Title, Type, Points, WorkRemaining, AssignedTo, Status
  - Drag-sort within sprint (`sprint_priority`)
- **Capacity check** — total points vs team velocity, total hours vs team availability
- **Quick sprint setup** — "Create Next Sprint" copies settings from previous
- **Sprint close** — marks incomplete items, option to carry forward to next sprint

### 2.3 Sprint Burndown Chart

- Points remaining vs ideal line (by day)
- Work remaining (hours) secondary axis
- Updates in real-time as tasks complete
- Historical sprint burndowns preserved

### 2.4 Velocity Tracking

- Bar chart: points completed per sprint (last N sprints)
- Rolling average line for forecasting
- Stored in `sprints.velocity_points` / `velocity_hours` on sprint close

### 2.5 Database Additions

```sql
-- Sprint capacity per user
CREATE TABLE sprint_allocations (
  id          serial PK,
  sprint_id   int → sprints NOT NULL,
  user_id     int → app_users NOT NULL,
  capacity_hours numeric(6,1),
  capacity_points numeric(6,1),
  UNIQUE(sprint_id, user_id)
);

-- Burndown snapshots (daily)
CREATE TABLE sprint_burndown (
  id          serial PK,
  sprint_id   int → sprints NOT NULL,
  snapshot_date date NOT NULL,
  points_remaining numeric(8,1),
  hours_remaining  numeric(8,1),
  points_completed numeric(8,1),
  hours_completed  numeric(8,1),
  UNIQUE(sprint_id, snapshot_date)
);
```

### 2.6 Deliverables

- [ ] TaskBacklogPanel with drag-sort priority
- [ ] Commit-to-sprint workflow (reference, not copy)
- [ ] SprintPlanPanel with capacity bar
- [ ] Sprint burndown chart (DX ChartControl)
- [ ] Velocity tracking chart
- [ ] Sprint create/close workflow
- [ ] Sprint allocations per user
- [ ] Daily burndown snapshot (background job or on-demand)
- [ ] Ribbon: Backlog + Sprint Planning panel toggles, sprint actions
- [ ] Context menus on both panels
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 3 — Kanban Board

**Goal:** Visual card-based board with configurable columns, WIP limits, swim-lanes,
and live drag-and-drop status transitions.

### 3.1 Kanban Board Panel (KanbanBoardPanel)

New DocumentPanel — card-based view.

- **Columns** mapped to task statuses (configurable per project)
  - Default: Backlog → To Do → In Progress → Review → Done
  - Custom columns via `board_columns` table
- **WIP limits** per column — header shows count/limit, red highlight when exceeded
- **Swim-lanes** — horizontal grouping (by assignee, priority, epic, or custom field)
- **Cards** display:
  - Color stripe (from task.color)
  - Title, type icon, priority icon
  - Assignee avatar/initials
  - Points badge
  - Attachment indicator
  - Due date (red if overdue)
- **Drag-and-drop** between columns updates `status` + `board_column`
- **Card click** opens task detail flyout or navigates to tree
- **Zoom levels** — compact (title only), normal, expanded (full detail)
- **Quick add** — "+" button in each column header

### 3.2 Board Configuration

```sql
CREATE TABLE board_columns (
  id          serial PK,
  project_id  int → projects NOT NULL,
  board_name  varchar(128) DEFAULT 'Default',
  column_name varchar(64) NOT NULL,
  status_mapping varchar(32),  -- maps to task.status
  sort_order  int DEFAULT 0,
  wip_limit   int,
  color       varchar(16),
  UNIQUE(project_id, board_name, column_name)
);

CREATE TABLE board_lanes (
  id          serial PK,
  project_id  int → projects NOT NULL,
  board_name  varchar(128) DEFAULT 'Default',
  lane_name   varchar(64) NOT NULL,
  lane_field  varchar(64),  -- field to group by (assigned_to, priority, epic, custom)
  sort_order  int DEFAULT 0
);
```

### 3.3 Flow Metrics

- **Cumulative flow diagram** — stacked area chart (items per status over time)
- **Cycle time** — time from In Progress to Done (per item, averaged)
- **Lead time** — time from creation to Done
- Stored as computed metrics, charted in dashboard

### 3.4 Deliverables

- [ ] KanbanBoardPanel with drag-and-drop columns
- [ ] WIP limits with visual indicators
- [ ] Swim-lane configuration
- [ ] Card rendering with all indicators
- [ ] Board column configuration (per project)
- [ ] Zoom levels (compact/normal/expanded)
- [ ] Quick add from column header
- [ ] Cumulative flow diagram
- [ ] Cycle time / lead time metrics
- [ ] Ribbon: Kanban Board panel toggle, board config button
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 4 — Gantt Scheduling & Dependencies

**Goal:** Full Gantt chart with dependency links, critical path, milestones,
schedule compression, and MS Project import.

### 4.1 Gantt Panel (GanttPanel)

New DocumentPanel — timeline-based view.

- **DevExpress GanttControl** (or custom DX chart) with:
  - Task bars colored by status or custom color
  - Milestone diamonds (zero-duration items)
  - Dependency arrows (FS/FF/SF/SS) with lag labels
  - Critical path highlighting (red bars/arrows)
  - Resource names on bars
  - Sprint containers as summary bars
- **Drag to reschedule** — move start/finish, auto-cascade dependents
- **Link creation** — draw arrow from task to task to create dependency
- **Zoom** — day / week / month / quarter / year
- **Today line** — vertical marker
- **Baseline comparison** — overlay original schedule vs current
- **High-res PDF export** — print-quality timeline

### 4.2 Critical Path Calculation

- Auto-calculated from dependency chain (longest path)
- Stored as `is_critical_path` boolean on each task (recalculated on dependency change)
- Highlighted in both Gantt and tree views

### 4.3 Schedule Compression

- **Fast-tracking** — overlap sequential tasks where possible
- **Crashing** — identify tasks where adding resources shortens duration
- Manual or auto-optimise button

### 4.4 MS Project Import

- Parse `.xml` (MS Project XML format)
- Map tasks, dependencies, milestones, resources
- Import wizard with preview + field mapping
- Update existing items on re-import (match by WBS or ID)

### 4.5 Database Additions

```sql
-- Baseline schedules for comparison
CREATE TABLE task_baselines (
  id            serial PK,
  task_id       int → tasks NOT NULL,
  baseline_name varchar(64) NOT NULL,
  start_date    date,
  finish_date   date,
  points        numeric(6,1),
  hours         numeric(8,1),
  saved_at      timestamptz DEFAULT now(),
  UNIQUE(task_id, baseline_name)
);
```

### 4.6 Deliverables

- [ ] GanttPanel with DX GanttControl or custom chart
- [ ] Task bars, milestones, dependency arrows
- [ ] Drag-to-reschedule with cascade
- [ ] Critical path calculation and highlighting
- [ ] Zoom levels (day/week/month/quarter/year)
- [ ] Today line
- [ ] Baseline save/compare overlay
- [ ] High-res PDF export
- [ ] MS Project XML import wizard
- [ ] Schedule compression (fast-track / crash)
- [ ] Ribbon: Gantt panel toggle, zoom controls, export, import
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 5 — Workflow Engine & Pipelines

**Goal:** Configurable workflows with custom statuses, transition rules, required
fields per transition, and reusable pipeline templates.

### 5.1 Workflow Engine

- **Multiple workflows per project** — different workflows for bugs vs features
- **Custom statuses** — any number of states with colors and icons
- **Transition rules** — define which status→status transitions are allowed
- **Required fields per transition** — e.g., "Moving to Done requires work_remaining = 0"
- **Auto-actions** — set field values on transition (e.g., set completed_at on Done)
- **Workflow assignment** — per task type or per individual task

### 5.2 Pipeline Templates

- **Ordered stage sequences** — each stage auto-generates a child task
- **Nested pipelines** — a pipeline stage can itself be a pipeline
- **Shared across projects** — template library
- **Default values per stage** — assignee, estimated hours, points

### 5.3 Database

```sql
CREATE TABLE workflows (
  id          serial PK,
  project_id  int → projects,           -- NULL = global template
  name        varchar(128) NOT NULL,
  description text,
  is_default  boolean DEFAULT false,
  created_at  timestamptz DEFAULT now()
);

CREATE TABLE workflow_statuses (
  id          serial PK,
  workflow_id int → workflows NOT NULL,
  name        varchar(64) NOT NULL,
  color       varchar(16),
  icon        varchar(32),
  sort_order  int DEFAULT 0,
  is_initial  boolean DEFAULT false,
  is_terminal boolean DEFAULT false
);

CREATE TABLE workflow_transitions (
  id              serial PK,
  workflow_id     int → workflows NOT NULL,
  from_status_id  int → workflow_statuses NOT NULL,
  to_status_id    int → workflow_statuses NOT NULL,
  required_fields text[],       -- field names that must be non-null
  auto_set_fields jsonb,        -- {"completed_at": "NOW", "work_remaining": 0}
  allowed_roles   text[],       -- role names that can perform this transition
  UNIQUE(workflow_id, from_status_id, to_status_id)
);

CREATE TABLE pipelines (
  id          serial PK,
  project_id  int → projects,
  name        varchar(128) NOT NULL,
  description text,
  is_template boolean DEFAULT false,
  created_at  timestamptz DEFAULT now()
);

CREATE TABLE pipeline_stages (
  id            serial PK,
  pipeline_id   int → pipelines NOT NULL,
  name          varchar(128) NOT NULL,
  sort_order    int DEFAULT 0,
  default_assignee int → app_users,
  default_hours numeric(6,1),
  default_points numeric(6,1),
  child_pipeline_id int → pipelines,  -- nested pipeline
  auto_set_fields jsonb
);
```

### 5.4 UI — Workflow Designer

- **WorkflowEditorPanel** — visual state machine editor
  - Drag-and-drop status nodes
  - Draw transition arrows between nodes
  - Click transition to set rules/required fields
- **Pipeline Editor** — ordered list of stages with drag reorder
  - Stage properties (defaults, nested pipeline selector)
- **Apply Pipeline** — right-click task → "Apply Pipeline" → generates child tasks from stages

### 5.5 Deliverables

- [ ] Workflow engine with custom statuses and transitions
- [ ] Required fields and auto-actions per transition
- [ ] Pipeline templates with nested pipelines
- [ ] Workflow designer panel (visual editor)
- [ ] Pipeline editor panel
- [ ] Apply pipeline to task (auto-generate children)
- [ ] Share workflows/pipelines across projects
- [ ] Task status dropdown respects workflow transitions
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 6 — QA & Issue Tracking

**Goal:** Dedicated bug tracking with severity/priority, bug-specific workflows,
triage capabilities, and integration with the sprint planning flow.

### 6.1 QA View Panel (QAPanel)

New DocumentPanel — bug/issue-centric grid.

- **Grid columns** — ID, Title, Severity, BugPriority, Status, AssignedTo, Reporter, Steps, Sprint
- **Bug-specific workflow** — New → Triaged → In Progress → Resolved → Verified → Closed
- **Severity vs Priority** — separate fields, both filterable
- **Steps to reproduce** — rich text field with screenshot paste
- **Batch triage** — multi-select + bulk update (severity, priority, assignee, sprint)
- **Link to tasks** — bugs link to features/stories via task_links
- **Commit bugs to sprints** alongside features

### 6.2 Bug Creation

- Quick-create from any view (toolbar or context menu)
- Auto-set `task_type = 'Bug'`, `severity`, `bug_priority`
- Clipboard paste for screenshots → auto-attach
- Bug template with steps_to_reproduce placeholder

### 6.3 QA Dashboard

- **Bug count by severity** (stacked bar)
- **Bug aging** (time since creation, bucketed)
- **Resolution rate** (opened vs closed over time)
- **Top reporters / assignees** (leaderboard)

### 6.4 Deliverables

- [ ] QAPanel with bug-specific grid and columns
- [ ] Bug workflow (New → Triaged → ... → Closed)
- [ ] Severity + BugPriority as separate fields
- [ ] Steps to reproduce rich text with screenshot paste
- [ ] Batch triage (multi-select bulk update)
- [ ] Bug creation from any view
- [ ] QA dashboard charts (severity, aging, resolution rate)
- [ ] Commit bugs to sprints
- [ ] Ribbon: QA panel toggle, triage actions
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 7 — Custom Columns & Field Permissions

**Goal:** Project-scoped custom fields with type-aware editors, aggregation,
and field-level access control.

### 7.1 Custom Column System

- **Main managers** create custom columns per project
- **Column types:** Text, Rich Text, Number, Hours, Drop List, Date, DateTime, People, Computed
- **Aggregation** on Number/Hours columns — Sum, Avg, Min, Max across children
- **Default values** — inherited by new items, overridable
- **Computed columns** — formula referencing other columns (read-only)

### 7.2 Database

```sql
CREATE TABLE custom_columns (
  id            serial PK,
  project_id    int → projects NOT NULL,
  name          varchar(64) NOT NULL,
  column_type   varchar(32) NOT NULL,  -- Text|RichText|Number|Hours|DropList|Date|DateTime|People|Computed
  config        jsonb,                  -- options for DropList, formula for Computed, aggregation type
  sort_order    int DEFAULT 0,
  default_value text,
  is_required   boolean DEFAULT false,
  UNIQUE(project_id, name)
);

CREATE TABLE custom_column_permissions (
  id          serial PK,
  column_id   int → custom_columns NOT NULL,
  user_id     int → app_users,          -- NULL = applies to group
  group_name  varchar(64),
  can_view    boolean DEFAULT true,
  can_edit    boolean DEFAULT true
);

CREATE TABLE task_custom_values (
  task_id     int → tasks NOT NULL,
  column_id   int → custom_columns NOT NULL,
  value_text  text,
  value_number numeric(12,4),
  value_date  timestamptz,
  value_json  jsonb,          -- for People[] or multi-value
  PRIMARY KEY(task_id, column_id)
);
```

### 7.3 UI — Column Manager

- **Custom Column Editor** — accessed from Project Settings
  - Add/edit/delete custom columns
  - Configure type, options (drop list values), formula, aggregation
  - Set field-level permissions (view/edit per user or group)
- **Dynamic grid columns** — custom columns appear in tree/grid views automatically
  - Type-aware editors (ComboBoxEdit for DropList, DateEdit for Date, etc.)
  - Aggregation in TotalSummary for numeric types

### 7.4 Deliverables

- [ ] Custom column CRUD per project
- [ ] 9 column types with type-aware editors
- [ ] Field-level permissions (view/edit per user/group)
- [ ] Dynamic column rendering in tree and grid views
- [ ] Aggregation (Sum/Avg/Min/Max) for Number/Hours columns
- [ ] Computed columns with formula engine
- [ ] Default value inheritance
- [ ] Custom Column Editor panel
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 8 — Reporting & Dashboards

**Goal:** Visual query builder, cross-project portfolio reports, chart wizard,
sharable dashboards, and export capabilities.

### 8.1 Report Engine

- **Visual query builder** — no SQL required
  - Select columns to display
  - Add filter conditions with typed operators (=, !=, >, <, contains, between)
  - Boolean logic: AND, OR, NOT, nested groups
  - Date relative queries: `Now-3M`, `Now+1W`, `FromDateToDate`
  - Sort and group by any column
- **Report folders** — organised, shared to users/groups
- **Cross-project reports** — query across multiple projects in a portfolio
- **Export** — Excel, CSV, XML

### 8.2 Dashboard Builder

- **Chart wizard** — right-click any container/report → "Chart This"
- **Chart types:** Bar, Pie, Line, XY/Scatter, Traffic Light, Text/List, Burndown
- **Dashboard pages** — configurable grid layout
  - Drag-resize chart tiles
  - Text/label widgets
  - Filter by project or saved report
- **Dashboard templates** — pre-built (Sprint Health, Portfolio Overview, QA Metrics, Team Velocity)
- **Historical trending** — charts track values over time
- **Sharing** — dashboards shared to users/groups
- **Export** — PDF, PNG, Excel, clipboard

### 8.3 Database

```sql
CREATE TABLE saved_reports (
  id          serial PK,
  project_id  int → projects,     -- NULL = cross-project
  name        varchar(128) NOT NULL,
  folder      varchar(128),
  query_json  jsonb NOT NULL,     -- columns, filters, sort, group
  created_by  int → app_users,
  shared_with jsonb,              -- [{"type":"user","id":1}, {"type":"group","name":"Dev"}]
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);

CREATE TABLE dashboards (
  id          serial PK,
  name        varchar(128) NOT NULL,
  layout_json jsonb NOT NULL,     -- tile positions, sizes, chart configs
  template    varchar(64),
  created_by  int → app_users,
  shared_with jsonb,
  created_at  timestamptz DEFAULT now(),
  updated_at  timestamptz DEFAULT now()
);

CREATE TABLE dashboard_snapshots (
  id            serial PK,
  dashboard_id  int → dashboards NOT NULL,
  snapshot_date date NOT NULL,
  data_json     jsonb NOT NULL,
  UNIQUE(dashboard_id, snapshot_date)
);
```

### 8.4 Deliverables

- [ ] Visual query builder with typed operators and boolean logic
- [ ] Date relative queries (Now±offset, ranges)
- [ ] Report save/share/folder organisation
- [ ] Cross-project portfolio reports
- [ ] Export to Excel, CSV, XML
- [ ] Dashboard builder with chart wizard
- [ ] 7 chart types
- [ ] Dashboard templates (4 pre-built)
- [ ] Historical trending via daily snapshots
- [ ] Dashboard sharing and export (PDF, PNG, Excel)
- [ ] Ribbon: Reports panel, Dashboard panel, chart actions
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 9 — Collaboration & Time Tracking

**Goal:** Real-time multiplayer editing, comments with @mentions, notifications,
built-in chat, activity feed, and integrated timesheet module.

### 9.1 Real-Time Collaboration

- **Live updates** — SignalR broadcasts task changes to all connected users
- **Presence indicators** — show who is viewing/editing each task (via Central.Collaboration PresenceService)
- **Conflict resolution** — last-write-wins with notification to other editors
- **Live cursor** — optional: show other users' selected row in tree/grid

### 9.2 Comments & Activity

- **Rich text comments** with @mentions (triggers notification)
- **Inline images** — paste from clipboard
- **File attachments** — upload and link to task
- **Activity feed** — live newsfeed of project activity (task created, status changed, commented)
- **Comment threading** — reply to specific comments

### 9.3 Notifications

- **Auto-watch** — items you create, comment on, or are assigned to
- **Email notifications** on watched-item changes (configurable)
- **Aggregated digest** — option for daily summary instead of per-change
- **In-app notification bell** — unread count badge, notification dropdown
- **Per-user preferences** — granular control (email/toast/none per event type)

### 9.4 Time Tracking

- **Log time** — against any task, with date, hours, activity type, notes
- **Timer** — start/stop timer on a task (auto-logs elapsed time)
- **Timesheet view** — personal weekly timesheet grid (days as columns, tasks as rows)
- **Manager view** — team timesheet summary
- **Portfolio capacity** — hours allocated vs logged across projects

### 9.5 Database

```sql
CREATE TABLE time_entries (
  id          serial PK,
  task_id     int → tasks NOT NULL,
  user_id     int → app_users NOT NULL,
  entry_date  date NOT NULL,
  hours       numeric(6,2) NOT NULL,
  activity_type varchar(32),   -- Development|Testing|Review|Meeting|Admin
  notes       text,
  created_at  timestamptz DEFAULT now()
);

CREATE INDEX idx_time_entries_user_date ON time_entries(user_id, entry_date);
CREATE INDEX idx_time_entries_task ON time_entries(task_id);

CREATE TABLE activity_feed (
  id          serial PK,
  project_id  int → projects,
  task_id     int → tasks,
  user_id     int → app_users,
  action      varchar(32) NOT NULL,   -- created|updated|commented|status_changed|assigned
  details     jsonb,                   -- field changes, comment excerpt, etc.
  created_at  timestamptz DEFAULT now()
);

CREATE INDEX idx_activity_feed_project ON activity_feed(project_id, created_at DESC);
```

### 9.6 Deliverables

- [ ] Real-time task updates via SignalR
- [ ] Presence indicators (who is viewing/editing)
- [ ] Rich text comments with @mentions and file attachments
- [ ] Activity feed panel (project-level newsfeed)
- [ ] Comment threading (reply to comment)
- [ ] Auto-watch + notification preferences
- [ ] Email digest (per-change or daily summary)
- [ ] In-app notification bell with unread count
- [ ] Time entry logging (manual + timer)
- [ ] Timesheet panel (personal weekly grid)
- [ ] Manager timesheet summary view
- [ ] Portfolio capacity view (allocated vs logged)
- [ ] Ribbon: Activity feed, Timesheet, Notifications panels
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 10 — To-Do Board, Portfolio View & Views Management

**Goal:** Personal to-do across all projects, cross-project portfolio lens,
saved views, and view propagation.

### 10.1 To-Do Board (MyTasksPanel)

- **Personal view** — everything assigned to current user, across all projects
- **Two modes:** prioritised checklist or card board layout
- **Quick inline editing** — status, work_remaining, time entry
- **Timesheet entry** directly from to-do items
- **Due date grouping** — Overdue / Today / This Week / Later / No Date

### 10.2 Portfolio View (PortfolioPanel)

- **Cross-project lens** — items from multiple projects side-by-side
- **Programme-level boards and backlogs**
- **Linked portfolio ↔ project backlogs** (drill from portfolio item to project item)
- **Resource capacity** across projects (who is overloaded, who has bandwidth)
- **Roll-up reporting** — total points, hours, completion % per project

### 10.3 Saved Views

```sql
CREATE TABLE task_views (
  id          serial PK,
  project_id  int → projects,
  name        varchar(128) NOT NULL,
  view_type   varchar(32) NOT NULL,  -- Tree|Grid|Board|Gantt|Backlog
  config_json jsonb NOT NULL,        -- columns, filters, sort, grouping, item visibility
  created_by  int → app_users,
  is_default  boolean DEFAULT false,
  shared_with jsonb,
  created_at  timestamptz DEFAULT now()
);
```

- Save current column set, filters, sort, grouping as a named view
- **Propagate views** — push a view to multiple users at once
- **Delegation** — assign backlog/planning sections to specific teams via view filters

### 10.4 Deliverables

- [ ] MyTasksPanel (personal to-do, two modes: list + board)
- [ ] Quick inline editing + timesheet entry from to-do
- [ ] Due date grouping (Overdue/Today/ThisWeek/Later)
- [ ] PortfolioPanel (cross-project roll-up)
- [ ] Resource capacity across projects
- [ ] Saved views (column set, filters, sort, grouping)
- [ ] View propagation to multiple users
- [ ] Delegation via view filters
- [ ] Ribbon: My Tasks, Portfolio panel toggles
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase 11 — Import/Export & Integrations

**Goal:** MS Project import, Excel import/export with column matching, clipboard
paste, PDF timeline export, API, webhooks, and external tool integrations.

### 11.1 Import

- **MS Project XML** — full import wizard (tasks, dependencies, milestones, resources)
  - Preview + field mapping before import
  - Update existing items on re-import (match by WBS or external ID)
- **Excel import** — auto-column-matching + update existing items
  - Detect headers, suggest field mapping
  - Preview changes before apply
- **Clipboard paste** — paste tabular data as new items (Ctrl+V in grid)

### 11.2 Export

- **Excel/CSV/XML** — any grid view exports with current filters/columns
- **High-res PDF** — Gantt timeline export (print-quality, configurable page layout)
- **Clipboard** — existing ExportGridToClipboard pattern

### 11.3 API & Webhooks

- **Full REST API** already scaffolded — extend with all new entities
- **Webhook outbound** — fire on task create/update/delete/status_change
  - Configure target URL, event filter, secret
- **Webhook inbound** — receive events from external tools (existing webhook_log infrastructure)

### 11.4 External Integrations (Future)

- **Git integration** — link tasks to commits/branches (via commit message parsing)
- **Jira sync** — bidirectional via existing sync_engine infrastructure
- **Slack notifications** — post task updates to Slack channels

### 11.5 Deliverables

- [ ] MS Project XML import wizard
- [ ] Excel import with auto-column-matching
- [ ] Clipboard paste-as-items
- [ ] Excel/CSV/XML export from any grid
- [ ] High-res PDF Gantt export
- [ ] Outbound webhooks (configurable per event)
- [ ] Inbound webhook handler for tasks
- [ ] Git commit → task link (commit message parsing)
- [ ] Jira sync via sync_engine (bidirectional)
- [ ] Ribbon: Import, Export, Integration settings
- [ ] Update FEATURE_TEST_CHECKLIST.md

---

## Phase Summary

| Phase | What | New Panels | Key Tables |
|-------|------|------------|------------|
| 1 | Hierarchy & schema expansion | — (expand existing) | portfolios, programmes, projects, sprints, releases, task_links, task_dependencies |
| 2 | Backlog & sprint planning | TaskBacklogPanel, SprintPlanPanel | sprint_allocations, sprint_burndown |
| 3 | Kanban board | KanbanBoardPanel | board_columns, board_lanes |
| 4 | Gantt scheduling | GanttPanel | task_baselines |
| 5 | Workflow engine & pipelines | WorkflowEditorPanel, PipelineEditorPanel | workflows, workflow_statuses, workflow_transitions, pipelines, pipeline_stages |
| 6 | QA & issue tracking | QAPanel | — (uses existing tasks + bug fields) |
| 7 | Custom columns & field permissions | CustomColumnEditor | custom_columns, custom_column_permissions, task_custom_values |
| 8 | Reporting & dashboards | ReportBuilderPanel, DashboardPanel | saved_reports, dashboards, dashboard_snapshots |
| 9 | Collaboration & time tracking | ActivityFeedPanel, TimesheetPanel | time_entries, activity_feed |
| 10 | To-Do, Portfolio, Views | MyTasksPanel, PortfolioPanel | task_views |
| 11 | Import/Export & Integrations | ImportWizard | — (uses existing sync_engine + webhook_log) |

### Dependencies Between Phases

```
Phase 1 ──┬── Phase 2 (needs projects, sprints)
           ├── Phase 3 (needs projects for board config)
           ├── Phase 4 (needs dependencies table)
           ├── Phase 5 (needs projects for workflows)
           ├── Phase 6 (needs bug fields from Phase 1 schema)
           └── Phase 7 (needs projects for custom columns)

Phase 2 ───── Phase 8 (burndown/velocity data for dashboards)
Phase 3 ───── Phase 8 (flow metrics for dashboards)
Phase 5 ───── Phase 6 (QA uses workflow engine)
Phase 1-7 ─── Phase 9 (collaboration spans all views)
Phase 1-9 ─── Phase 10 (portfolio aggregates everything)
Phase 1-10 ── Phase 11 (import/export needs full schema)
```

### Build Order Recommendation

**Phase 1 is prerequisite for everything.** After Phase 1, phases 2-7 can be built
in any order (or in parallel). Phases 8-11 benefit from earlier phases being complete
but can start with partial data.

Recommended sequence: **1 → 2 → 3 → 5 → 6 → 4 → 7 → 8 → 9 → 10 → 11**

Rationale:
- Backlog + Sprint (2) gives immediate agile workflow value
- Kanban (3) is a quick visual win after backlog exists
- Workflows (5) before QA (6) since QA depends on the workflow engine
- Gantt (4) is high-effort, benefits from stable schema
- Custom columns (7) extends everything built so far
- Reporting (8) needs data from phases 2-7 to be meaningful
- Collaboration (9) and Portfolio (10) layer on top of everything
- Import/Export (11) is the final polish
