import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, VlanListRow, AuditRow, DhcpRelayTargetRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.vlan detail page — companion to the thin /network/vlans grid.
/// Three tabs: Summary (VLAN row fields), Audit (last 100 entries),
/// DHCP relays (every net.dhcp_relay_target pointing at this VLAN).
///
/// Routed at /network/net-vlan/:id. The DHCP relays tab matters
/// here because VLANs typically carry one or two relay targets
/// (primary + peer) and operators often chase "which helpers does
/// this VLAN render" when diagnosing DHCP issues.
@Component({
  selector: 'app-network-vlan-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/vlans" class="back-link">← VLANs</a>
      <h2 *ngIf="vlan">VLAN {{ vlan.vlanId }} · {{ vlan.displayName }}</h2>
      <h2 *ngIf="!vlan">Loading…</h2>
      <small *ngIf="vlan" class="subtitle">
        net.vlan · {{ vlan.blockCode ?? '(no block)' }} · {{ vlan.scopeLevel }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && vlan" class="meta-grid">
      <div class="meta-row"><label>VLAN tag</label>    <span>{{ vlan.vlanId }}</span></div>
      <div class="meta-row"><label>Display name</label><span>{{ vlan.displayName }}</span></div>
      <div class="meta-row"><label>Block</label>       <span>{{ vlan.blockCode ?? '—' }}</span></div>
      <div class="meta-row"><label>Scope level</label> <span>{{ vlan.scopeLevel }}</span></div>
      <div class="meta-row"><label>Status</label>      <span>{{ vlan.status }}</span></div>
      <div class="meta-row"><label>Version</label>     <span>{{ vlan.version }}</span></div>
      <div class="meta-row"><label>UUID</label>        <code>{{ vlan.id }}</code></div>

      <div class="meta-row">
        <label>DHCP relays</label>
        <span>{{ relayCount === null ? '…' : relayCount }}</span>
      </div>
      <div class="meta-row">
        <label>Linked subnets</label>
        <span>{{ subnetCount === null ? '…' : subnetCount }}</span>
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

    <!-- DHCP relays tab -->
    <div *ngIf="activeTab === 2">
      <div class="toolbar">
        <a routerLink="/network/dhcp-relay" class="link">Manage DHCP relay targets →</a>
      </div>
      <dx-data-grid [dataSource]="relays" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true">
        <dxi-column dataField="serverIp" caption="Server IP" width="160"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="priority" caption="Priority"  width="90"  dataType="number" />
        <dxi-column dataField="status"   caption="Status"    width="90" />
        <dxi-column dataField="version"  caption="v"         width="50" />
        <dxi-column dataField="notes"    caption="Notes" />
        <dxi-column dataField="id"       caption="UUID"      width="260" />
      </dx-data-grid>
      <div *ngIf="relays.length === 0 && !loadingRelays" class="empty-note">
        No DHCP relay targets configured for this VLAN.
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
    .toolbar { display: flex; gap: 12px; margin-bottom: 8px; }
    .link { color: #60a5fa; text-decoration: none; font-size: 12px; }
    .link:hover { text-decoration: underline; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .empty-note { margin-top: 12px; padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkVlanDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }, { text: 'DHCP relays' }];
  activeTab = 0;

  vlanId = '';
  vlan: VlanListRow | null = null;
  audit: AuditRow[] = [];
  relays: DhcpRelayTargetRow[] = [];
  loadingRelays = false;
  /// Summary count enrichment — populated on page load via the
  /// listDhcpRelayTargets(vlanId) narrower + client-side filter of
  /// listSubnets for vlanTag match. Null = still loading.
  relayCount: number | null = null;
  subnetCount: number | null = null;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.vlanId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.vlanId) {
      this.status = 'Missing route param — expected /network/net-vlan/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listVlans(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.vlan = rows.find(r => r.id === this.vlanId) ?? null;
        this.status = this.vlan ? '' : 'VLAN not found.';
        if (this.vlan) {
          this.loadTabData();
          this.loadSummaryCounts();
        }
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  /// Cache each tab's data on first view.
  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'Vlan', this.vlanId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
    if (this.activeTab === 2 && this.relays.length === 0 && !this.loadingRelays) {
      this.loadingRelays = true;
      this.engine.listDhcpRelayTargets(environment.defaultTenantId, this.vlanId).subscribe({
        next: (rows) => { this.relays = rows; this.loadingRelays = false; },
        error: () => { this.loadingRelays = false; },
      });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }

  /// Summary-tab enrichment — fires two parallel calls on page
  /// load so operators see "DHCP relays: 2 · Linked subnets: 1"
  /// without opening the DHCP tab. listDhcpRelayTargets takes a
  /// vlanId narrower; subnet count uses client-side vlanTag
  /// filter because listSubnets lacks a direct vlan-id narrower.
  private loadSummaryCounts(): void {
    if (!this.vlan) { return; }
    // Pre-populate this.relays so the DHCP tab doesn't re-fetch.
    this.engine.listDhcpRelayTargets(environment.defaultTenantId, this.vlanId).subscribe({
      next: (rows) => {
        this.relays = rows ?? [];
        this.relayCount = this.relays.length;
      },
      error: () => { this.relayCount = 0; },
    });
    const tag = this.vlan.vlanId;
    this.engine.listSubnets(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.subnetCount = (rows ?? []).filter(r => r.vlanTag === tag).length;
      },
      error: () => { this.subnetCount = 0; },
    });
  }
}
