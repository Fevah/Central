import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ServerListRow, AuditRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.server detail page — companion to the thin /network/servers
/// grid. Two tabs for this slice: Summary (server row + metadata),
/// Audit (last 100 audit entries). NICs + allocation-fanout detail
/// would be a third tab once the engine thin list for net.server_nic
/// lands.
///
/// Routed at /network/net-server/:id. Grid double-click drills here
/// instead of straight to audit (symmetrical with the device detail
/// page).
@Component({
  selector: 'app-network-server-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/servers" class="back-link">← Servers</a>
      <h2 *ngIf="server">{{ server.hostname }}</h2>
      <h2 *ngIf="!server">Loading…</h2>
      <small *ngIf="server" class="subtitle">
        net.server · {{ server.profileCode ?? '(no profile)' }} · {{ server.buildingCode ?? '(no building)' }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && server" class="meta-grid">
      <div class="meta-row"><label>Hostname</label>     <span>{{ server.hostname }}</span></div>
      <div class="meta-row"><label>Profile code</label> <span>{{ server.profileCode ?? '—' }}</span></div>
      <div class="meta-row"><label>Building code</label><span>{{ server.buildingCode ?? '—' }}</span></div>
      <div class="meta-row"><label>Status</label>       <span>{{ server.status }}</span></div>
      <div class="meta-row"><label>Version</label>      <span>{{ server.version }}</span></div>
      <div class="meta-row"><label>UUID</label>         <code>{{ server.id }}</code></div>
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
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .tab-bar { margin: 12px 0; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkServerDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }];
  activeTab = 0;

  serverId = '';
  server: ServerListRow | null = null;
  audit: AuditRow[] = [];
  status = '';

  constructor(
    private route: ActivatedRoute,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.serverId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.serverId) {
      this.status = 'Missing route param — expected /network/net-server/:id.';
      return;
    }
    // Fetch from thin list + filter client-side. Same pattern as
    // device-detail; fine for ~5k-row tenants, a per-uuid endpoint
    // lands if the cap becomes an issue.
    this.status = 'Loading…';
    this.engine.listServers(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.server = rows.find(r => r.id === this.serverId) ?? null;
        this.status = this.server ? '' : 'Server not found.';
        if (this.server) this.loadTabData();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'Server', this.serverId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }
}
