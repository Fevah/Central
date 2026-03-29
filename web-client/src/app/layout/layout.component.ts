import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { DxDrawerModule, DxListModule, DxToolbarModule } from 'devextreme-angular';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDrawerModule, DxListModule, DxToolbarModule],
  template: `
    <dx-drawer [opened]="true" [openedStateMode]="'shrink'" [position]="'left'" [revealMode]="'slide'"
               template="sidebar" [minSize]="56">
      <div *dxTemplate="let data of 'sidebar'" class="sidebar">
        <div class="sidebar-header">
          <span class="logo">C</span>
          <span class="logo-text">Central</span>
        </div>
        <dx-list [items]="navItems" [hoverStateEnabled]="true" [activeStateEnabled]="true"
                 [focusStateEnabled]="false" (onItemClick)="navigate($event)">
          <div *dxTemplate="let item of 'item'" class="nav-item">
            <i [class]="item.icon"></i>
            <span>{{ item.text }}</span>
          </div>
        </dx-list>
      </div>

      <div class="main-content">
        <dx-toolbar class="main-toolbar">
          <dxi-item location="before">
            <div class="toolbar-title">{{ currentTitle }}</div>
          </dxi-item>
          <dxi-item location="after">
            <div class="user-info">
              <span>{{ user?.display_name }}</span>
              <button class="logout-btn" (click)="logout()">Logout</button>
            </div>
          </dxi-item>
        </dx-toolbar>

        <div class="content-area">
          <router-outlet></router-outlet>
        </div>
      </div>
    </dx-drawer>
  `,
  styles: [`
    .sidebar { background: #111827; height: 100vh; width: 240px; }
    .sidebar-header { padding: 16px; display: flex; align-items: center; gap: 10px; border-bottom: 1px solid #1f2937; }
    .logo { background: #3b82f6; color: white; width: 32px; height: 32px; border-radius: 8px; display: flex; align-items: center; justify-content: center; font-weight: bold; font-size: 16px; }
    .logo-text { color: white; font-size: 18px; font-weight: 600; }
    .nav-item { display: flex; align-items: center; gap: 12px; padding: 8px 16px; color: #d1d5db; }
    .nav-item i { width: 20px; text-align: center; }
    .main-content { height: 100vh; display: flex; flex-direction: column; }
    .main-toolbar { padding: 0 16px; border-bottom: 1px solid #1f2937; }
    .toolbar-title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .content-area { flex: 1; padding: 16px; overflow: auto; background: #0f172a; }
    .user-info { display: flex; align-items: center; gap: 12px; color: #9ca3af; }
    .logout-btn { background: #374151; color: #d1d5db; border: none; padding: 4px 12px; border-radius: 4px; cursor: pointer; }
  `]
})
export class LayoutComponent {
  currentTitle = 'Dashboard';

  navItems = [
    { text: 'Dashboard', icon: 'dx-icon-chart', path: '/' },
    { text: 'Tasks', icon: 'dx-icon-todo', path: '/tasks' },
    { text: 'Kanban', icon: 'dx-icon-columnchooser', path: '/kanban' },
    { text: 'Network', icon: 'dx-icon-globe', path: '/network' },
    { text: 'Service Desk', icon: 'dx-icon-email', path: '/servicedesk' },
    { text: 'Admin', icon: 'dx-icon-preferences', path: '/admin' },
  ];

  get user() { return this.auth.currentUser; }

  constructor(private auth: AuthService, private router: Router) {}

  navigate(e: any): void {
    const item = e.itemData;
    this.currentTitle = item.text;
    this.router.navigate([item.path]);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
