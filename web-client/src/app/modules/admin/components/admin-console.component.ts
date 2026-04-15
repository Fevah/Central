import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  DxDataGridModule, DxButtonModule, DxTabPanelModule,
  DxFormModule, DxPopupModule, DxToolbarModule, DxChartModule
} from 'devextreme-angular';
import { AdminService, PlatformDashboard, ServiceStatus, Tenant, GlobalUser, ModuleLicense } from '../../../core/services/admin.service';
import { PermissionCode, PermissionService } from '../../../core/services/permission.service';
import { ApiKeysPanelComponent } from './api-keys-panel.component';
import { JobsPanelComponent }    from './jobs-panel.component';
import { BackupsPanelComponent } from './backups-panel.component';
import { UsersPanelComponent }   from './users-panel.component';
import { confirm } from 'devextreme/ui/dialog';
import notify from 'devextreme/ui/notify';

@Component({
  selector: 'app-admin-console',
  standalone: true,
  imports: [
    CommonModule, DxDataGridModule, DxButtonModule, DxTabPanelModule,
    DxFormModule, DxPopupModule, DxToolbarModule, DxChartModule,
    ApiKeysPanelComponent, JobsPanelComponent, BackupsPanelComponent, UsersPanelComponent,
  ],
  template: `
    <!-- System Info Bar -->
    <div class="system-bar" *ngIf="dashboard">
      <div class="system-item"><span class="sys-label">Platform</span><span class="sys-value">{{ dashboard.system.platform }}</span></div>
      <div class="system-item"><span class="sys-label">Services</span><span class="sys-value">{{ services.length }}</span></div>
      <div class="system-item"><span class="sys-label">Tenants</span><span class="sys-value">{{ tenants.length }}</span></div>
      <div class="system-item">
        <span class="sys-label">Health</span>
        <span class="sys-value" [class.healthy]="allHealthy" [class.degraded]="!allHealthy">
          {{ allHealthy ? '● All Healthy' : '● Degraded' }}
        </span>
      </div>
      <dx-button text="Refresh" icon="refresh" (onClick)="refresh()" [stylingMode]="'text'" />
    </div>

    <!-- Tabs are filtered server-side by user permissions in visibleTabs. -->
    <dx-tab-panel [items]="visibleTabs" [selectedIndex]="0" [animationEnabled]="true" [swipeEnabled]="true">
      <div *dxTemplate="let tab of 'item'">
        <ng-container [ngSwitch]="tab.key">

          <!-- Service Health -->
          <div *ngSwitchCase="'health'">
            <dx-data-grid [dataSource]="services" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" height="calc(100vh - 280px)">
              <dxi-column dataField="name" caption="Service" width="160" />
              <dxi-column dataField="url" caption="URL" width="250" />
              <dxi-column dataField="healthy" caption="Status" width="100" cellTemplate="statusTpl" />
              <dxi-column dataField="version" caption="Version" width="100" />
              <dxi-column dataField="latency_ms" caption="Latency" width="100" dataType="number" />
              <div *dxTemplate="let d of 'statusTpl'">
                <span [class]="d.value ? 'st-ok' : 'st-err'">{{ d.value ? '● Healthy' : '● Down' }}</span>
              </div>
            </dx-data-grid>
          </div>

          <!-- Tenants (global admin only) -->
          <div *ngSwitchCase="'tenants'">
            <dx-toolbar style="margin-bottom: 8px;">
              <dxi-item location="before" widget="dxButton"
                        [options]="{ text: '+ New Tenant', type: 'default', onClick: openNewTenant }"></dxi-item>
            </dx-toolbar>
            <dx-data-grid [dataSource]="tenants" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                          height="calc(100vh - 320px)">
              <dxi-column dataField="id" caption="ID" width="80" />
              <dxi-column dataField="name" caption="Tenant" width="200" />
              <dxi-column dataField="slug" caption="Slug" width="120" />
              <dxi-column dataField="status" caption="Status" width="100" cellTemplate="tenantStatusTpl" />
              <dxi-column dataField="plan" caption="Plan" width="100" />
              <dxi-column dataField="user_count" caption="Users" width="70" />
              <dxi-column dataField="created_at" caption="Created" width="140" dataType="datetime" />
              <dxi-column caption="Actions" width="180" cellTemplate="tenantActionsTpl" />

              <div *dxTemplate="let d of 'tenantStatusTpl'">
                <span [class]="'badge t-' + (d.value || 'unknown').toLowerCase()">{{ d.value }}</span>
              </div>
              <div *dxTemplate="let d of 'tenantActionsTpl'">
                <dx-button *ngIf="d.data.status === 'active'" text="Suspend" [stylingMode]="'outlined'" type="danger"
                           (onClick)="suspendTenant(d.data)" />
                <dx-button *ngIf="d.data.status === 'suspended'" text="Activate" [stylingMode]="'outlined'" type="success"
                           (onClick)="activateTenant(d.data)" />
              </div>
            </dx-data-grid>
          </div>

          <!-- Tenant Users (CRUD + password reset) -->
          <div *ngSwitchCase="'tenant-users'">
            <app-users-panel></app-users-panel>
          </div>

          <!-- Global Users (cross-tenant view, global admin) -->
          <div *ngSwitchCase="'global-users'">
            <dx-data-grid [dataSource]="users" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                          [searchPanel]="{ visible: true }"
                          height="calc(100vh - 280px)">
              <dxi-column dataField="email" caption="Email" width="220" />
              <dxi-column dataField="display_name" caption="Name" width="160" />
              <dxi-column dataField="tenant_name" caption="Tenant" width="140" />
              <dxi-column dataField="roles" caption="Roles" width="120" />
              <dxi-column dataField="status" caption="Status" width="100" cellTemplate="userStatusTpl" />
              <dxi-column dataField="mfa_enabled" caption="MFA" width="60" dataType="boolean" />
              <dxi-column dataField="last_login" caption="Last Login" width="140" dataType="datetime" />

              <div *dxTemplate="let d of 'userStatusTpl'">
                <span [class]="'badge u-' + (d.value || 'unknown').toLowerCase()">{{ d.value }}</span>
              </div>
            </dx-data-grid>
          </div>

          <!-- API Keys -->
          <div *ngSwitchCase="'api-keys'">
            <app-api-keys-panel></app-api-keys-panel>
          </div>

          <!-- Jobs -->
          <div *ngSwitchCase="'jobs'">
            <app-jobs-panel></app-jobs-panel>
          </div>

          <!-- Backups -->
          <div *ngSwitchCase="'backups'">
            <app-backups-panel></app-backups-panel>
          </div>

          <!-- Licenses -->
          <div *ngSwitchCase="'licenses'">
            <dx-data-grid [dataSource]="licenses" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                          [editing]="{ mode: 'cell', allowUpdating: true }"
                          (onRowUpdated)="onLicenseUpdated($event)"
                          height="calc(100vh - 280px)">
              <dxi-column dataField="tenant_name" caption="Tenant" width="160" [allowEditing]="false" />
              <dxi-column dataField="module_name" caption="Module" width="140" [allowEditing]="false" />
              <dxi-column dataField="enabled" caption="Enabled" width="80" dataType="boolean" />
              <dxi-column dataField="max_users" caption="Max Users" width="100" dataType="number" />
              <dxi-column dataField="expires_at" caption="Expires" width="140" dataType="date" />
            </dx-data-grid>
          </div>

          <!-- Infrastructure -->
          <div *ngSwitchCase="'infra'">
            <dx-data-grid [dataSource]="routeTable" [showBorders]="true" [rowAlternationEnabled]="true"
                          [columnAutoWidth]="true" height="calc(100vh - 280px)">
              <dxi-column dataField="name" caption="Route" width="160" />
              <dxi-column dataField="path_prefix" caption="Path" width="200" />
              <dxi-column dataField="url" caption="Backend" width="280" />
              <dxi-column dataField="timeout_secs" caption="Timeout" width="80" />
              <dxi-column dataField="healthy" caption="Active" width="80" dataType="boolean" />
            </dx-data-grid>
          </div>

        </ng-container>
      </div>
    </dx-tab-panel>

    <!-- New Tenant Dialog -->
    <dx-popup [visible]="showNewTenant" [dragEnabled]="true" [showCloseButton]="true"
              title="New Tenant" [width]="480" [height]="380" (onHidden)="showNewTenant = false">
      <dx-form [(formData)]="newTenant" [colCount]="1" labelLocation="top">
        <dxi-item dataField="name" [editorOptions]="{ placeholder: 'Company name' }">
          <dxi-validation-rule type="required" />
        </dxi-item>
        <dxi-item dataField="slug" [editorOptions]="{ placeholder: 'url-slug (auto-generated)' }" />
        <dxi-item dataField="plan" editorType="dxSelectBox" [editorOptions]="{ items: ['free','pro','enterprise'], value: 'free' }" />
        <dxi-item dataField="admin_email" [editorOptions]="{ placeholder: 'admin@company.com' }">
          <dxi-validation-rule type="required" />
          <dxi-validation-rule type="email" />
        </dxi-item>
        <dxi-item itemType="button" [buttonOptions]="{ text: 'Create Tenant', type: 'default', width: '100%', onClick: submitTenant }" />
      </dx-form>
    </dx-popup>
  `,
  styles: [`
    .system-bar { display: flex; gap: 24px; align-items: center; background: #1e293b; padding: 12px 20px; border-radius: 8px; margin-bottom: 16px; flex-wrap: wrap; }
    .system-item { display: flex; flex-direction: column; } .sys-label { font-size: 11px; color: #6b7280; }
    .sys-value { font-size: 15px; color: #f9fafb; font-weight: 600; } .healthy { color: #22c55e; } .degraded { color: #ef4444; }
    .st-ok { color: #22c55e; font-weight: 600; } .st-err { color: #ef4444; font-weight: 600; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .t-active { background: rgba(34,197,94,0.2); color: #22c55e; } .t-suspended { background: rgba(239,68,68,0.2); color: #ef4444; }
    .u-active { background: rgba(34,197,94,0.2); color: #22c55e; } .u-locked { background: rgba(239,68,68,0.2); color: #ef4444; }
    .u-pending { background: rgba(234,179,8,0.2); color: #eab308; }
  `]
})
export class AdminConsoleComponent implements OnInit, OnDestroy {
  dashboard: PlatformDashboard | null = null;
  services: ServiceStatus[] = [];
  tenants: Tenant[] = [];
  users: GlobalUser[] = [];
  licenses: ModuleLicense[] = [];
  routeTable: any[] = [];
  allHealthy = false;
  showNewTenant = false;
  newTenant: any = {};
  private refreshInterval: any;

  /**
   * All tabs with their required permissions. Each tab is shown only if the
   * current user has *any* of the listed permissions. Tabs marked with `null`
   * are unconditional (Service Health is always visible to anyone reaching
   * /admin — the route guard already requires the `admin` module license).
   *
   * Tenant-only views (api-keys, jobs, backups, tenant-users) gate by their
   * `admin:*` permission. Cross-tenant views (tenants, global-users, licenses)
   * stay until we wire `is_global_admin` into the user payload — for now they
   * use the `admin:roles` permission as a stand-in for "global admin role".
   */
  private readonly allTabs: TabDef[] = [
    { title: 'Service Health', key: 'health',        perms: null },
    { title: 'Users',          key: 'tenant-users',  perms: ['admin:users'] },
    { title: 'API Keys',       key: 'api-keys',      perms: ['admin:keys'] },
    { title: 'Jobs',           key: 'jobs',          perms: ['admin:jobs'] },
    { title: 'Backups',        key: 'backups',       perms: ['admin:backups'] },
    { title: 'Tenants',        key: 'tenants',       perms: ['admin:roles'] },
    { title: 'Global Users',   key: 'global-users',  perms: ['admin:roles'] },
    { title: 'Licenses',       key: 'licenses',      perms: ['admin:roles'] },
    { title: 'Infrastructure', key: 'infra',         perms: null },
  ];

  visibleTabs: TabDef[] = [];

  constructor(private admin: AdminService, private permissions: PermissionService) {}

  ngOnInit(): void {
    this.rebuildTabs();
    this.refresh();
    this.refreshInterval = setInterval(() => this.refresh(), 30000);
  }

  private rebuildTabs(): void {
    this.visibleTabs = this.allTabs.filter(t =>
      t.perms === null || this.permissions.hasAny(...t.perms)
    );
  }
  ngOnDestroy(): void { if (this.refreshInterval) clearInterval(this.refreshInterval); }

  refresh(): void {
    this.admin.getDashboard().subscribe(d => {
      this.dashboard = d; this.services = d.services; this.allHealthy = d.services.every(s => s.healthy);
    });
    this.admin.getServices().subscribe(s => this.routeTable = s);
    this.admin.getTenants().subscribe(t => this.tenants = t);
    this.admin.getGlobalUsers().subscribe(u => this.users = u);
    this.admin.getLicenses().subscribe(l => this.licenses = l);
  }

  openNewTenant = (): void => { this.newTenant = { name: '', slug: '', plan: 'free', admin_email: '' }; this.showNewTenant = true; };

  submitTenant = (): void => {
    if (!this.newTenant.name?.trim()) { notify('Name required', 'warning', 2000); return; }
    this.admin.createTenant(this.newTenant).subscribe({
      next: () => { notify('Tenant created', 'success', 3000); this.showNewTenant = false; this.refresh(); },
      error: () => notify('Failed to create tenant', 'error', 3000)
    });
  };

  suspendTenant(t: Tenant): void {
    confirm(`Suspend tenant "${t.name}"?`, 'Confirm').then(ok => {
      if (ok) this.admin.updateTenantStatus(t.id, 'suspended').subscribe(() => { notify('Suspended', 'success', 2000); this.refresh(); });
    });
  }

  activateTenant(t: Tenant): void {
    this.admin.updateTenantStatus(t.id, 'active').subscribe(() => { notify('Activated', 'success', 2000); this.refresh(); });
  }

  onLicenseUpdated(e: any): void {
    this.admin.updateLicense(e.key.id, e.data).subscribe({
      next: () => notify('License updated', 'success', 2000),
      error: () => notify('Update failed', 'error', 3000)
    });
  }
}

interface TabDef {
  title: string;
  key:   string;
  perms: PermissionCode[] | null;
}
