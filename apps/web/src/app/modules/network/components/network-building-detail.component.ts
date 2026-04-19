import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, BuildingRow, DeviceListRow, ServerListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Building detail — hero page for "what's in this building?".
/// Three tabs: Summary (row fields), Devices (every net.device
/// whose buildingCode matches), Servers (every net.server whose
/// buildingCode matches).
///
/// Distinct from the audit-timeline drill — this is the operational
/// "building scorecard" page. Routed at /network/building/:id.
/// The hierarchy page links here from the Building node on the tree.
///
/// Building row carries PascalCase fields because /api/net/buildings
/// is served by Central.Api (.NET) rather than the engine. The
/// thin device + server lists are engine-served (camelCase), so
/// this page mixes both casings — one of the few places that
/// surfaces on the UI.
@Component({
  selector: 'app-network-building-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/hierarchy" class="back-link">← Hierarchy</a>
      <h2 *ngIf="building">{{ building.BuildingCode }} · {{ building.DisplayName }}</h2>
      <h2 *ngIf="!building">Loading…</h2>
      <small *ngIf="building" class="subtitle">Building · {{ building.Status }}</small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && building" class="meta-grid">
      <div class="meta-row"><label>Building code</label><span>{{ building.BuildingCode }}</span></div>
      <div class="meta-row"><label>Display name</label> <span>{{ building.DisplayName }}</span></div>
      <div class="meta-row"><label>Site id</label>      <code>{{ building.SiteId }}</code></div>
      <div class="meta-row"><label>Status</label>       <span>{{ building.Status }}</span></div>
      <div class="meta-row full"><label>UUID</label>    <code>{{ building.Id }}</code></div>

      <!-- Quick counts — these cost nothing extra because the
           tabs load eagerly on first view; we reflect the latest
           count back to the summary for scan-ability. -->
      <div class="meta-row" *ngIf="devices.length > 0 || serversLoaded">
        <label>Devices</label>
        <span>{{ devices.length }}</span>
      </div>
      <div class="meta-row" *ngIf="servers.length > 0 || serversLoaded">
        <label>Servers</label>
        <span>{{ servers.length }}</span>
      </div>
    </div>

    <!-- Devices tab -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="devices" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     [headerFilter]="{ visible: true }"
                     (onRowDblClick)="onDeviceDblClick($event)">
        <dxi-column dataField="hostname" caption="Hostname" [fixed]="true" width="200"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="roleCode" caption="Role"     width="120" />
        <dxi-column dataField="status"   caption="Status"   width="90" />
        <dxi-column dataField="version"  caption="v"        width="50" dataType="number" />
        <dxi-column dataField="id"       caption="UUID" />
      </dx-data-grid>
      <div *ngIf="devices.length === 0 && !loadingDevices" class="empty-note">
        No devices in this building.
      </div>
    </div>

    <!-- Servers tab -->
    <div *ngIf="activeTab === 2">
      <dx-data-grid [dataSource]="servers" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     [headerFilter]="{ visible: true }"
                     (onRowDblClick)="onServerDblClick($event)">
        <dxi-column dataField="hostname"    caption="Hostname" [fixed]="true" width="200"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="profileCode" caption="Profile"  width="130" />
        <dxi-column dataField="status"      caption="Status"   width="90" />
        <dxi-column dataField="version"     caption="v"        width="50" dataType="number" />
        <dxi-column dataField="id"          caption="UUID" />
      </dx-data-grid>
      <div *ngIf="servers.length === 0 && !loadingServers" class="empty-note">
        No servers in this building.
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .tab-bar { margin: 12px 0; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .empty-note { margin-top: 12px; padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkBuildingDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Devices' }, { text: 'Servers' }];
  activeTab = 0;

  buildingId = '';
  building: BuildingRow | null = null;
  devices: DeviceListRow[] = [];
  servers: ServerListRow[] = [];
  loadingDevices = false;
  loadingServers = false;
  /// Tracks whether the servers tab has completed its load — used
  /// by the Summary tab's counts row so it shows "0 servers" rather
  /// than hiding the row when the tab hasn't been visited yet.
  serversLoaded = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.buildingId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.buildingId) {
      this.status = 'Missing route param — expected /network/building/:id.';
      return;
    }
    this.status = 'Loading…';

    // Buildings list is tenant-wide; filter client-side.
    this.engine.listBuildings().subscribe({
      next: (rows) => {
        this.building = rows.find(r => r.Id === this.buildingId) ?? null;
        this.status = this.building ? '' : 'Building not found.';
        if (this.building) {
          // Eager-load devices + servers so the Summary counts are
          // populated before the operator clicks the tabs. Two
          // cheap thin-list calls.
          this.loadDevices();
          this.loadServers();
        }
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  private loadDevices(): void {
    if (!this.building) return;
    this.loadingDevices = true;
    this.engine.listDevices(environment.defaultTenantId).subscribe({
      next: (rows) => {
        const code = this.building!.BuildingCode;
        this.devices = rows.filter(d => d.buildingCode === code);
        this.loadingDevices = false;
      },
      error: () => { this.loadingDevices = false; },
    });
  }

  private loadServers(): void {
    if (!this.building) return;
    this.loadingServers = true;
    this.engine.listServers(environment.defaultTenantId).subscribe({
      next: (rows) => {
        const code = this.building!.BuildingCode;
        this.servers = rows.filter(s => s.buildingCode === code);
        this.loadingServers = false;
        this.serversLoaded = true;
      },
      error: () => { this.loadingServers = false; this.serversLoaded = true; },
    });
  }

  onTabChanged(_e: unknown): void { /* tabs already hydrated eagerly */ }

  onDeviceDblClick(e: { data: DeviceListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/net-device', row.id]);
  }

  onServerDblClick(e: { data: ServerListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/net-server', row.id]);
  }
}
