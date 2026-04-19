import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { moduleGuard } from './core/guards/module.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./modules/auth/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    loadComponent: () => import('./layout/layout.component').then(m => m.LayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: '',       loadComponent: () => import('./modules/dashboard/components/dashboard.component').then(m => m.DashboardComponent) },

      // Tasks (module-gated). `tasks` module powers everything in this group.
      { path: 'tasks',           canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/task-tree.component').then(m => m.TaskTreeComponent) },
      { path: 'kanban',          canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/kanban-board.component').then(m => m.KanbanBoardComponent) },
      { path: 'tasks/sprints',   canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/sprint-board.component').then(m => m.SprintBoardComponent) },
      { path: 'tasks/burndown',  canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/burndown.component').then(m => m.BurndownComponent) },
      { path: 'tasks/gantt',     canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/gantt.component').then(m => m.GanttComponent) },
      { path: 'tasks/timesheet', canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/timesheet.component').then(m => m.TimesheetComponent) },
      { path: 'tasks/activity',  canActivate: [moduleGuard('tasks')],
        loadComponent: () => import('./modules/tasks/components/activity.component').then(m => m.ActivityComponent) },

      // Network = switches + devices + links + routing. Grouping them behind
      // the `switches` license (the base module); individual pages additionally
      // check their own module (links, routing) for fine-grained gating.
      {
        path: 'network',
        canActivate: [moduleGuard('switches')],
        children: [
          { path: '',                loadComponent: () => import('./modules/network/components/network-dashboard.component').then(m => m.NetworkDashboardComponent) },
          { path: 'devices/:id',     loadComponent: () => import('./modules/network/components/device-detail.component').then(m => m.DeviceDetailComponent) },
          { path: 'switches/:host',  loadComponent: () => import('./modules/network/components/switch-detail.component').then(m => m.SwitchDetailComponent) },
          { path: 'links',           canActivate: [moduleGuard('links')],
            loadComponent: () => import('./modules/network/components/link-grid.component').then(m => m.LinkGridComponent) },
          { path: 'bgp',             canActivate: [moduleGuard('routing')],
            loadComponent: () => import('./modules/network/components/bgp-peers.component').then(m => m.BgpPeersComponent) },
          { path: 'search',          loadComponent: () => import('./modules/network/components/network-search.component').then(m => m.NetworkSearchComponent) },
          { path: 'validation',      loadComponent: () => import('./modules/network/components/network-validation.component').then(m => m.NetworkValidationComponent) },
          { path: 'scope-grants',    loadComponent: () => import('./modules/network/components/network-scope-grants.component').then(m => m.NetworkScopeGrantsComponent) },
          { path: 'hierarchy',       loadComponent: () => import('./modules/network/components/network-hierarchy.component').then(m => m.NetworkHierarchyComponent) },
          { path: 'pools',           loadComponent: () => import('./modules/network/components/network-pools.component').then(m => m.NetworkPoolsComponent) },
          { path: 'bulk',            loadComponent: () => import('./modules/network/components/network-bulk.component').then(m => m.NetworkBulkComponent) },
          { path: 'devices',         loadComponent: () => import('./modules/network/components/network-devices.component').then(m => m.NetworkDevicesComponent) },
          { path: 'net-device/:id',  loadComponent: () => import('./modules/network/components/network-device-detail.component').then(m => m.NetworkDeviceDetailComponent) },
          { path: 'net-server/:id',  loadComponent: () => import('./modules/network/components/network-server-detail.component').then(m => m.NetworkServerDetailComponent) },
          { path: 'net-vlan/:id',    loadComponent: () => import('./modules/network/components/network-vlan-detail.component').then(m => m.NetworkVlanDetailComponent) },
          { path: 'net-link/:id',    loadComponent: () => import('./modules/network/components/network-link-detail.component').then(m => m.NetworkLinkDetailComponent) },
          { path: 'net-subnet/:id',  loadComponent: () => import('./modules/network/components/network-subnet-detail.component').then(m => m.NetworkSubnetDetailComponent) },
          { path: 'net-dhcp-relay/:id', loadComponent: () => import('./modules/network/components/network-dhcp-relay-detail.component').then(m => m.NetworkDhcpRelayDetailComponent) },
          { path: 'building/:id',    loadComponent: () => import('./modules/network/components/network-building-detail.component').then(m => m.NetworkBuildingDetailComponent) },
          { path: 'site/:id',        loadComponent: () => import('./modules/network/components/network-site-detail.component').then(m => m.NetworkSiteDetailComponent) },
          { path: 'region/:id',      loadComponent: () => import('./modules/network/components/network-region-detail.component').then(m => m.NetworkRegionDetailComponent) },
          { path: 'floor/:id',       loadComponent: () => import('./modules/network/components/network-floor-detail.component').then(m => m.NetworkFloorDetailComponent) },
          { path: 'vlans',           loadComponent: () => import('./modules/network/components/network-vlans.component').then(m => m.NetworkVlansComponent) },
          { path: 'servers',         loadComponent: () => import('./modules/network/components/network-servers.component').then(m => m.NetworkServersComponent) },
          { path: 'links-grid',      loadComponent: () => import('./modules/network/components/network-links.component').then(m => m.NetworkLinksGridComponent) },
          { path: 'subnets',         loadComponent: () => import('./modules/network/components/network-subnets.component').then(m => m.NetworkSubnetsComponent) },
          { path: 'ports',           loadComponent: () => import('./modules/network/components/network-ports.component').then(m => m.NetworkPortsComponent) },
          { path: 'aggregate-ethernet', loadComponent: () => import('./modules/network/components/network-aggregate-ethernet.component').then(m => m.NetworkAggregateEthernetComponent) },
          { path: 'dhcp-relay',      loadComponent: () => import('./modules/network/components/network-dhcp-relay.component').then(m => m.NetworkDhcpRelayComponent) },
          { path: 'change-sets',     loadComponent: () => import('./modules/network/components/network-change-sets.component').then(m => m.NetworkChangeSetsComponent) },
          { path: 'change-sets/:id', loadComponent: () => import('./modules/network/components/network-change-set-detail.component').then(m => m.NetworkChangeSetDetailComponent) },
          { path: 'locks',           loadComponent: () => import('./modules/network/components/network-locks.component').then(m => m.NetworkLocksComponent) },
          { path: 'naming-preview',  loadComponent: () => import('./modules/network/components/network-naming-preview.component').then(m => m.NetworkNamingPreviewComponent) },
          { path: 'naming-overrides',loadComponent: () => import('./modules/network/components/network-naming-overrides.component').then(m => m.NetworkNamingOverridesComponent) },
          { path: 'render-history',  loadComponent: () => import('./modules/network/components/network-render-history.component').then(m => m.NetworkRenderHistoryComponent) },
          { path: 'render-pack',     loadComponent: () => import('./modules/network/components/network-render-pack.component').then(m => m.NetworkRenderPackComponent) },
          { path: 'pool-utilization',loadComponent: () => import('./modules/network/components/network-pool-utilization.component').then(m => m.NetworkPoolUtilizationComponent) },
          { path: 'cli-flavors',     loadComponent: () => import('./modules/network/components/network-cli-flavors.component').then(m => m.NetworkCliFlavorsComponent) },
          { path: 'audit-stats',     loadComponent: () => import('./modules/network/components/network-audit-stats.component').then(m => m.NetworkAuditStatsComponent) },
          { path: 'audit-search',    loadComponent: () => import('./modules/network/components/network-audit-search.component').then(m => m.NetworkAuditSearchComponent) },
          { path: 'audit/:entityType/:entityId',
            loadComponent: () => import('./modules/network/components/network-audit-timeline.component').then(m => m.NetworkAuditTimelineComponent) },
        ]
      },

      { path: 'servicedesk', canActivate: [moduleGuard('servicedesk')],
        loadComponent: () => import('./modules/servicedesk/components/servicedesk.component').then(m => m.ServiceDeskComponent) },
      { path: 'audit',       canActivate: [moduleGuard('audit')],
        loadComponent: () => import('./modules/audit/components/audit-dashboard.component').then(m => m.AuditDashboardComponent) },
      { path: 'admin',       canActivate: [moduleGuard('admin')],
        loadComponent: () => import('./modules/admin/components/admin-console.component').then(m => m.AdminConsoleComponent) },
    ]
  },
  { path: '**', redirectTo: '' }
];
