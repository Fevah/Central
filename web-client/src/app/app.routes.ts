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
