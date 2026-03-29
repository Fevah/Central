import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { AdminService, PlatformDashboard, ServiceStatus } from '../../../core/services/admin.service';

@Component({
  selector: 'app-admin-console',
  standalone: true,
  imports: [CommonModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="admin-console">
      <!-- System Info -->
      <div class="system-bar" *ngIf="dashboard">
        <div class="system-item">
          <span class="sys-label">Platform</span>
          <span class="sys-value">{{ dashboard.system.platform }}</span>
        </div>
        <div class="system-item">
          <span class="sys-label">Gateway</span>
          <span class="sys-value">v{{ dashboard.system.gateway_version }}</span>
        </div>
        <div class="system-item">
          <span class="sys-label">Routes</span>
          <span class="sys-value">{{ dashboard.system.total_routes }}</span>
        </div>
        <div class="system-item">
          <span class="sys-label">Status</span>
          <span class="sys-value" [class.healthy]="allHealthy" [class.degraded]="!allHealthy">
            {{ allHealthy ? 'All Healthy' : 'Degraded' }}
          </span>
        </div>
        <dx-button text="Refresh" icon="refresh" (onClick)="refresh()" [stylingMode]="'text'" />
      </div>

      <!-- Service Health Grid -->
      <h3>Service Health</h3>
      <dx-data-grid [dataSource]="services" [showBorders]="true" [rowAlternationEnabled]="true"
                      [columnAutoWidth]="true" height="300">
        <dxi-column dataField="name" caption="Service" width="160" />
        <dxi-column dataField="url" caption="URL" width="250" />
        <dxi-column dataField="healthy" caption="Status" width="100" cellTemplate="statusTemplate" />
        <dxi-column dataField="version" caption="Version" width="100" />
        <dxi-column dataField="latency_ms" caption="Latency (ms)" width="120" dataType="number" />

        <div *dxTemplate="let cell of 'statusTemplate'">
          <span [class.status-ok]="cell.value" [class.status-down]="!cell.value">
            {{ cell.value ? '● Healthy' : '● Down' }}
          </span>
        </div>
      </dx-data-grid>

      <!-- Route Table -->
      <h3 class="mt-4">Route Table</h3>
      <dx-data-grid [dataSource]="routeTable" [showBorders]="true" [rowAlternationEnabled]="true"
                      [columnAutoWidth]="true" [filterRow]="{ visible: true }" height="400">
        <dxi-column dataField="name" caption="Route" width="160" />
        <dxi-column dataField="path_prefix" caption="Path Prefix" width="200" />
        <dxi-column dataField="url" caption="Backend URL" width="280" />
        <dxi-column dataField="timeout_secs" caption="Timeout (s)" width="100" />
        <dxi-column dataField="healthy" caption="Active" width="80" dataType="boolean" />
      </dx-data-grid>
    </div>
  `,
  styles: [`
    .admin-console { padding: 0; }
    .system-bar { display: flex; gap: 24px; align-items: center; background: #1e293b; padding: 12px 20px; border-radius: 8px; margin-bottom: 16px; flex-wrap: wrap; }
    .system-item { display: flex; flex-direction: column; }
    .sys-label { font-size: 11px; color: #6b7280; }
    .sys-value { font-size: 15px; color: #f9fafb; font-weight: 600; }
    .healthy { color: #22c55e; }
    .degraded { color: #ef4444; }
    h3 { color: #f9fafb; margin: 0 0 8px 0; font-size: 15px; }
    .mt-4 { margin-top: 20px; }
    .status-ok { color: #22c55e; font-weight: 600; }
    .status-down { color: #ef4444; font-weight: 600; }
  `]
})
export class AdminConsoleComponent implements OnInit, OnDestroy {
  dashboard: PlatformDashboard | null = null;
  services: ServiceStatus[] = [];
  routeTable: any[] = [];
  allHealthy = false;
  private refreshInterval: any;

  constructor(private admin: AdminService) {}

  ngOnInit(): void {
    this.refresh();
    this.refreshInterval = setInterval(() => this.refresh(), 30000);
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) clearInterval(this.refreshInterval);
  }

  refresh(): void {
    this.admin.getDashboard().subscribe(d => {
      this.dashboard = d;
      this.services = d.services;
      this.allHealthy = d.services.every(s => s.healthy);
    });
    this.admin.getServices().subscribe(s => this.routeTable = s);
  }
}
