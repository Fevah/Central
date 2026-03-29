import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./modules/auth/login.component').then(m => m.LoginComponent) },
  {
    path: '',
    loadComponent: () => import('./layout/layout.component').then(m => m.LayoutComponent),
    canActivate: [authGuard],
    children: [
      { path: '', loadComponent: () => import('./modules/dashboard/components/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'tasks', loadComponent: () => import('./modules/tasks/components/task-tree.component').then(m => m.TaskTreeComponent) },
      { path: 'kanban', loadComponent: () => import('./modules/tasks/components/kanban-board.component').then(m => m.KanbanBoardComponent) },
      { path: 'admin', loadComponent: () => import('./modules/admin/components/admin-console.component').then(m => m.AdminConsoleComponent) },
    ]
  },
  { path: '**', redirectTo: '' }
];
