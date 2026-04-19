import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, PortListRow, AuditRow,
  LinkEndpointListRow, ServerNicListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Port detail page — three tabs: Summary + Audit + Usage
/// (link endpoints + server NICs pointing at this port).
/// Usage tab answers "what's wired into this port?" — a common
/// operator question when chasing a cable.
///
/// Routed at /network/port/:id.
@Component({
  selector: 'app-network-port-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/ports" class="back-link">← Ports</a>
      <h2 *ngIf="port">{{ port.interfaceName }}</h2>
      <h2 *ngIf="!port">Loading…</h2>
      <small *ngIf="port" class="subtitle">
        on <a [routerLink]="['/network/net-device', port.deviceId]" class="device-link">{{ port.deviceHostname ?? port.deviceId }}</a>
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && port" class="meta-grid">
      <div class="meta-row"><label>Interface</label>    <code>{{ port.interfaceName }}</code></div>
      <div class="meta-row"><label>Prefix</label>       <span>{{ port.interfacePrefix }}</span></div>
      <div class="meta-row"><label>Speed (Mb)</label>   <span>{{ port.speedMbps ?? '— (auto)' }}</span></div>
      <div class="meta-row"><label>Admin up</label>     <span>{{ port.adminUp }}</span></div>
      <div class="meta-row"><label>Port mode</label>    <span>{{ port.portMode }}</span></div>
      <div class="meta-row"><label>Native VLAN</label>  <span>{{ port.nativeVlanId ?? '—' }}</span></div>
      <div class="meta-row"><label>Aggregate</label>
        <a *ngIf="port.aggregateEthernetId"
           [routerLink]="['/network/aggregate-ethernet', port.aggregateEthernetId]"
           class="ae-link">{{ port.aggregateEthernetId }}</a>
        <span *ngIf="!port.aggregateEthernetId">—</span>
      </div>
      <div class="meta-row"><label>Status</label>       <span>{{ port.status }}</span></div>
      <div class="meta-row full" *ngIf="port.description"><label>Description</label><span>{{ port.description }}</span></div>
      <div class="meta-row full"><label>UUID</label>    <code>{{ port.id }}</code></div>
    </div>

    <!-- Audit tab — uses the Port entity type. Thin audit rows
         today; populated as the engine adds emission for port
         mutations. -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="audit" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true">
        <dxi-column dataField="sequenceId"    caption="Seq"         width="70" dataType="number"
                    sortOrder="desc" [sortIndex]="0" />
        <dxi-column dataField="createdAt"     caption="At"          width="170" dataType="datetime"
                    format="yyyy-MM-dd HH:mm:ss" />
        <dxi-column dataField="action"        caption="Action"      width="120" />
        <dxi-column dataField="actorDisplay"  caption="Actor"       width="150" />
        <dxi-column dataField="correlationId" caption="Correlation" width="240" />
      </dx-data-grid>
    </div>

    <!-- Usage tab — link endpoints + server NICs wired at this port. -->
    <div *ngIf="activeTab === 2">
      <h3 class="section-title">Link endpoints ({{ linkEndpoints.length }})</h3>
      <dx-data-grid *ngIf="linkEndpoints.length > 0"
                     [dataSource]="linkEndpoints" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true">
        <dxi-column dataField="linkCode"      caption="Link code"  width="200" />
        <dxi-column dataField="endpointOrder" caption="Order"      width="80"  dataType="number" />
        <dxi-column dataField="deviceHostname" caption="Device"    width="180" />
        <dxi-column dataField="ipAddress"     caption="IP"         width="140" />
        <dxi-column dataField="vlanTag"       caption="VLAN"       width="80"  dataType="number" />
        <dxi-column dataField="status"        caption="Status"     width="90" />
      </dx-data-grid>
      <div *ngIf="linkEndpoints.length === 0 && !loadingUsage" class="empty-note">
        No links use this port.
      </div>

      <h3 class="section-title">Server NICs ({{ serverNics.length }})</h3>
      <dx-data-grid *ngIf="serverNics.length > 0"
                     [dataSource]="serverNics" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true">
        <dxi-column dataField="serverHostname" caption="Server"   width="180" />
        <dxi-column dataField="nicIndex"       caption="NIC #"    width="80"  dataType="number" />
        <dxi-column dataField="nicName"        caption="NIC name" width="140" />
        <dxi-column dataField="mlagSide"       caption="Side"     width="70" />
        <dxi-column dataField="ipAddress"      caption="IP"       width="140" />
        <dxi-column dataField="status"         caption="Status"   width="90" />
      </dx-data-grid>
      <div *ngIf="serverNics.length === 0 && !loadingUsage" class="empty-note">
        No server NICs on this port.
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; font-family: ui-monospace, monospace; }
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
    .device-link, .ae-link { color: #60a5fa; text-decoration: none; }
    .device-link:hover, .ae-link:hover { text-decoration: underline; }
    .section-title { color: #9ca3af; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; margin: 16px 0 8px; font-weight: 600; }
    .empty-note { padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkPortDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }, { text: 'Usage' }];
  activeTab = 0;

  portId = '';
  port: PortListRow | null = null;
  audit: AuditRow[] = [];
  linkEndpoints: LinkEndpointListRow[] = [];
  serverNics: ServerNicListRow[] = [];
  loadingUsage = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private _router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.portId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.portId) {
      this.status = 'Missing route param — expected /network/port/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listPorts(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.port = rows.find(r => r.id === this.portId) ?? null;
        this.status = this.port ? '' : 'Port not found.';
        if (this.port) this.loadTabData();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'Port', this.portId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
    if (this.activeTab === 2 && !this.loadingUsage && this.linkEndpoints.length === 0 && this.serverNics.length === 0) {
      this.loadingUsage = true;
      // Pull tenant-wide endpoint + NIC lists + filter by portId.
      // The LEFT JOINs in those thin lists already resolve the port
      // via port_id / target_port_id so filtering is cheap.
      this.engine.listLinkEndpoints(environment.defaultTenantId).subscribe({
        next: (rows) => {
          // Engine's LinkEndpointListRow doesn't currently carry
          // the port_id uuid on the wire (only the interface_name
          // string), so filter by (link's device hostname +
          // interface match the port's). Crude; a future engine
          // change to surface portId on the row would tighten this.
          if (this.port) {
            const port = this.port;
            this.linkEndpoints = rows.filter(r =>
              r.deviceHostname === port.deviceHostname &&
              (r.portInterface === port.interfaceName ||
               r.interfaceName === port.interfaceName));
          }
          this.loadingUsage = false;
        },
        error: () => { this.loadingUsage = false; },
      });
      this.engine.listServerNics(environment.defaultTenantId).subscribe({
        next: (rows) => {
          if (this.port) {
            const port = this.port;
            this.serverNics = rows.filter(r =>
              r.targetDeviceHostname === port.deviceHostname &&
              r.targetPortInterface === port.interfaceName);
          }
        },
        error: () => {},
      });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }
}
