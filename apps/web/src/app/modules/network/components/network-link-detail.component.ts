import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, LinkListRow, AuditRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.link detail page — companion to the thin /network/links-grid.
/// Two tabs: Summary (link row with both endpoints) + Audit (last
/// 100 entries). A third "Endpoints" tab with port + interface
/// breakdown lands when the engine gains a thin
/// /api/net/link-endpoints endpoint; the thin link list already
/// carries deviceA + deviceB hostnames which cover the common case.
///
/// Routed at /network/net-link/:id.
@Component({
  selector: 'app-network-link-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/links-grid" class="back-link">← Links</a>
      <h2 *ngIf="link">{{ link.linkCode }}</h2>
      <h2 *ngIf="!link">Loading…</h2>
      <small *ngIf="link" class="subtitle">
        net.link · {{ link.linkType ?? '(untyped)' }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && link" class="summary-block">
      <div class="endpoints-grid">
        <div class="endpoint-card">
          <div class="endpoint-label">A-side</div>
          <div class="endpoint-device">
            <a *ngIf="link.deviceA" href="javascript:void(0)"
               class="device-link"
               (click)="drillDevice(link.deviceA)">{{ link.deviceA }}</a>
            <span *ngIf="!link.deviceA">—</span>
          </div>
        </div>

        <div class="endpoint-arrow">↔</div>

        <div class="endpoint-card">
          <div class="endpoint-label">B-side</div>
          <div class="endpoint-device">
            <a *ngIf="link.deviceB" href="javascript:void(0)"
               class="device-link"
               (click)="drillDevice(link.deviceB)">{{ link.deviceB }}</a>
            <span *ngIf="!link.deviceB">—</span>
          </div>
        </div>
      </div>

      <div class="meta-grid">
        <div class="meta-row"><label>Link code</label>  <span>{{ link.linkCode }}</span></div>
        <div class="meta-row"><label>Type</label>       <span>{{ link.linkType ?? '—' }}</span></div>
        <div class="meta-row"><label>Status</label>     <span>{{ link.status }}</span></div>
        <div class="meta-row"><label>Version</label>    <span>{{ link.version }}</span></div>
        <div class="meta-row full"><label>UUID</label>  <code>{{ link.id }}</code></div>
      </div>
    </div>

    <!-- Audit tab -->
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
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; font-family: ui-monospace, monospace; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .tab-bar { margin: 12px 0; }
    .summary-block { display: flex; flex-direction: column; gap: 16px; }
    .endpoints-grid { display: grid; grid-template-columns: 1fr auto 1fr; gap: 16px; align-items: center; padding: 20px; background: #1e293b; border-radius: 6px; }
    .endpoint-card { padding: 16px; background: #0f172a; border-radius: 6px; text-align: center; }
    .endpoint-label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 6px; }
    .endpoint-device { font-size: 18px; font-weight: 600; color: #cbd5e1; font-family: ui-monospace, monospace; }
    .endpoint-arrow { color: #60a5fa; font-size: 32px; font-weight: bold; }
    .device-link { color: #60a5fa; text-decoration: none; }
    .device-link:hover { text-decoration: underline; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    @media (max-width: 900px) {
      .endpoints-grid { grid-template-columns: 1fr; }
      .endpoint-arrow { transform: rotate(90deg); }
      .meta-grid { grid-template-columns: 1fr 1fr; }
    }
  `]
})
export class NetworkLinkDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }];
  activeTab = 0;

  linkId = '';
  link: LinkListRow | null = null;
  audit: AuditRow[] = [];
  status = '';

  /// Hostname → net.device uuid map, filled lazily when the user
  /// clicks an endpoint. Cheaper than fetching the full device list
  /// up-front — only does the lookup if needed.
  private deviceHostnameMap: Map<string, string> | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.linkId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.linkId) {
      this.status = 'Missing route param — expected /network/net-link/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listLinks(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.link = rows.find(r => r.id === this.linkId) ?? null;
        this.status = this.link ? '' : 'Link not found.';
        if (this.link) this.loadTabData();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'Link', this.linkId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }

  /// Endpoint hostname → net.device detail page. Builds the
  /// hostname-to-uuid map on first click (cached thereafter).
  drillDevice(hostname: string): void {
    if (!hostname) return;
    if (this.deviceHostnameMap) {
      this.goToDevice(hostname);
      return;
    }
    this.engine.listDevices(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.deviceHostnameMap = new Map(
          rows.map(d => [d.hostname.toLowerCase(), d.id]));
        this.goToDevice(hostname);
      },
      error: () => { this.status = `Couldn't resolve ${hostname} — device list load failed.`; },
    });
  }

  private goToDevice(hostname: string): void {
    const uuid = this.deviceHostnameMap?.get(hostname.toLowerCase());
    if (!uuid) {
      this.status = `Device '${hostname}' not found in net.device.`;
      return;
    }
    this.router.navigate(['/network/net-device', uuid]);
  }
}
