import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { DxDrawerModule, DxListModule, DxToolbarModule } from 'devextreme-angular';
import { Subscription, merge } from 'rxjs';
import { AuthService } from '../core/services/auth.service';
import { ModuleCode, ModuleRegistryService } from '../core/services/module-registry.service';
import { PermissionCode, PermissionService } from '../core/services/permission.service';
import { SignalRService } from '../core/services/signalr.service';
import { SseService } from '../core/services/sse.service';

interface NavItem {
  text:   string;
  icon:   string;
  path:   string;
  /** Module license required to show this item. `null` = always visible. */
  module: ModuleCode | null;
  /** Permission(s) — user needs *any* of these. `null` = no permission gate. */
  perms:  PermissionCode[] | null;
}

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
        <dx-list [items]="visibleNavItems" [hoverStateEnabled]="true" [activeStateEnabled]="true"
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
              <!-- Realtime connection status — green=connected, amber=reconnecting, grey=down -->
              <span class="rt-status" [class]="'rt-' + (rtState$ | async)" [title]="'Realtime: ' + (rtState$ | async)">
                <span class="rt-dot"></span>
              </span>
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
    .rt-status { display: inline-flex; align-items: center; }
    .rt-status .rt-dot { width: 8px; height: 8px; border-radius: 50%; background: #6b7280; }
    .rt-connected     .rt-dot { background: #22c55e; }
    .rt-connecting    .rt-dot { background: #eab308; animation: rt-pulse 1.4s ease-in-out infinite; }
    .rt-reconnecting  .rt-dot { background: #f59e0b; animation: rt-pulse 1s ease-in-out infinite; }
    .rt-disconnected  .rt-dot { background: #6b7280; }
    @keyframes rt-pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.3; } }
  `]
})
export class LayoutComponent implements OnInit, OnDestroy {
  currentTitle = 'Dashboard';

  /**
   * Full nav definition. Each item declares the module license + permissions
   * required to show it. `null` on either field means "no gate".
   *
   *   module: tenant-level licensing (ModuleRegistryService)
   *   perms:  user-level RBAC (PermissionService) — user needs ANY of them
   *
   * Both must allow before the item shows. Dashboard is unconditional.
   */
  private readonly allNavItems: NavItem[] = [
    { text: 'Dashboard',    icon: 'dx-icon-chart',         path: '/',            module: null,         perms: null },
    { text: 'Tasks',        icon: 'dx-icon-todo',          path: '/tasks',       module: 'tasks',      perms: null },
    { text: 'Kanban',       icon: 'dx-icon-columnchooser', path: '/kanban',      module: 'tasks',      perms: null },
    { text: 'Network',      icon: 'dx-icon-globe',         path: '/network',     module: 'switches',
      perms: ['switches:read', 'devices:read', 'links:read', 'bgp:read'] },
    { text: 'Service Desk', icon: 'dx-icon-email',         path: '/servicedesk', module: 'servicedesk', perms: null },
    { text: 'Audit',        icon: 'dx-icon-find',          path: '/audit',       module: 'audit',
      perms: ['admin:audit'] },
    { text: 'Admin',        icon: 'dx-icon-preferences',   path: '/admin',       module: 'admin',
      perms: ['admin:users', 'admin:roles', 'admin:lookups', 'admin:settings',
              'admin:keys',  'admin:jobs',  'admin:backups'] },
  ];

  /** Items the current tenant actually sees — filtered by ModuleRegistry. */
  visibleNavItems: NavItem[] = [];

  /** Realtime connection state for the toolbar indicator (set in constructor). */
  rtState$!: typeof this.signalR.state$;

  private sub?: Subscription;

  get user() { return this.auth.currentUser; }

  constructor(
    private auth: AuthService,
    private router: Router,
    private modules: ModuleRegistryService,
    private perms: PermissionService,
    private signalR: SignalRService,
    private sse: SseService,
  ) {
    this.rtState$ = this.signalR.state$;
  }

  ngOnInit(): void {
    // Rebuild nav when either the licensed-module list OR the user changes —
    // covers login, logout, role change, and runtime license toggles.
    this.sub = merge(this.modules.modules$, this.auth.user$)
      .subscribe(() => this.rebuildNav());

    // Layout only mounts when authenticated (authGuard enforces this), so
    // this is the right hook to bring up realtime channels. We also re-
    // connect on user change in case the token rotated.
    this.sub.add(
      this.auth.user$.subscribe(u => u ? this.openRealtime() : this.closeRealtime())
    );
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    // The user is leaving the authed shell — tear down sockets too.
    this.closeRealtime();
  }

  private async openRealtime(): Promise<void> {
    // Fire and forget — both services have their own reconnect logic, so
    // failures here aren't fatal. ErrorLogger captures the underlying errors.
    try { await this.signalR.connect(); } catch { /* ignore */ }
    try { this.sse.connect();          } catch { /* ignore */ }
  }

  private async closeRealtime(): Promise<void> {
    try { await this.signalR.disconnect(); } catch { /* ignore */ }
    try { this.sse.disconnect();           } catch { /* ignore */ }
  }

  private rebuildNav(): void {
    this.visibleNavItems = this.allNavItems.filter(i => {
      const moduleOk = i.module === null || this.modules.isEnabled(i.module);
      const permOk   = i.perms  === null || this.perms.hasAny(...i.perms);
      return moduleOk && permOk;
    });
  }

  navigate(e: any): void {
    const item = e.itemData as NavItem;
    this.currentTitle = item.text;
    this.router.navigate([item.path]);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
