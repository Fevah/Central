import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, DhcpRelayTargetRow, AuditRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.dhcp_relay_target detail page — closes the entity-detail
/// set (device / server / vlan / link / subnet / dhcp-relay). Two
/// tabs: Summary (relay row + resolved VLAN context) + Audit
/// (last 100 entries via /api/net/audit/entity/DhcpRelayTarget/:uuid).
///
/// Routed at /network/net-dhcp-relay/:id. VLAN shown as a clickable
/// link to the VLAN detail page — common drill for "what other
/// relays does this VLAN carry?".
@Component({
  selector: 'app-network-dhcp-relay-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/dhcp-relay" class="back-link">← DHCP relay targets</a>
      <h2 *ngIf="relay">{{ vlanLabel }} → {{ relay.serverIp }}</h2>
      <h2 *ngIf="!relay">Loading…</h2>
      <small *ngIf="relay" class="subtitle">
        net.dhcp_relay_target · priority {{ relay.priority }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && relay" class="meta-grid">
      <div class="meta-row">
        <label>VLAN</label>
        <a *ngIf="vlanUuid" href="javascript:void(0)"
           class="vlan-link" (click)="drillVlan()">{{ vlanLabel }}</a>
        <span *ngIf="!vlanUuid">{{ vlanLabel }}</span>
      </div>
      <div class="meta-row"><label>Server IP</label>   <span>{{ relay.serverIp }}</span></div>
      <div class="meta-row"><label>Priority</label>    <span>{{ relay.priority }}</span></div>
      <div class="meta-row"><label>Status</label>      <span>{{ relay.status }}</span></div>
      <div class="meta-row"><label>Version</label>     <span>{{ relay.version }}</span></div>
      <div class="meta-row"><label>Created</label>     <span>{{ formatTs(relay.createdAt) }}</span></div>
      <div class="meta-row"><label>Updated</label>     <span>{{ formatTs(relay.updatedAt) }}</span></div>
      <div class="meta-row full" *ngIf="relay.notes">
        <label>Notes</label>
        <span>{{ relay.notes }}</span>
      </div>
      <div class="meta-row full">
        <label>UUID</label>
        <code>{{ relay.id }}</code>
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
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .vlan-link { color: #60a5fa; text-decoration: none; }
    .vlan-link:hover { text-decoration: underline; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkDhcpRelayDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }];
  activeTab = 0;

  relayId = '';
  relay: DhcpRelayTargetRow | null = null;
  audit: AuditRow[] = [];
  status = '';

  /// Resolved VLAN uuid + human label. The relay row carries
  /// only vlanId (uuid); the thin VLAN list is fetched once to
  /// render "VLAN 120 · Servers" in the meta grid + page header.
  vlanUuid: string | null = null;
  vlanLabel = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.relayId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.relayId) {
      this.status = 'Missing route param — expected /network/net-dhcp-relay/:id.';
      return;
    }
    this.status = 'Loading…';

    // Fetch the relay list + VLAN list in parallel. The relay list
    // is tenant-wide (no per-uuid endpoint) so we filter client-side;
    // VLAN list resolves the label.
    this.engine.listDhcpRelayTargets(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.relay = rows.find(r => r.id === this.relayId) ?? null;
        if (!this.relay) { this.status = 'Relay target not found.'; return; }
        this.status = '';
        this.resolveVlan();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  private resolveVlan(): void {
    if (!this.relay) return;
    this.vlanUuid = this.relay.vlanId;
    this.vlanLabel = this.relay.vlanId; // fallback to uuid until resolved
    this.engine.listVlans(environment.defaultTenantId).subscribe({
      next: (vlans) => {
        const v = vlans.find(x => x.id === this.relay?.vlanId);
        if (v) this.vlanLabel = `VLAN ${v.vlanId} · ${v.displayName}`;
      },
      error: () => {},
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(
        environment.defaultTenantId, 'DhcpRelayTarget', this.relayId, 100,
      ).subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }

  drillVlan(): void {
    if (!this.vlanUuid) return;
    this.router.navigate(['/network/net-vlan', this.vlanUuid]);
  }

  formatTs(iso: string): string {
    if (!iso) return '—';
    try { return new Date(iso).toLocaleString(); } catch { return iso; }
  }
}
