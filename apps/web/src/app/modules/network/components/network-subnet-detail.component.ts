import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, SubnetListRow, AuditRow, IpAddressListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.subnet detail page — fifth entity detail after device /
/// server / VLAN / link. Three tabs: Summary (row fields) + Audit
/// (last 100 entries) + Addresses (IPs allocated from this subnet).
///
/// Routed at /network/net-subnet/:id. The Addresses tab is the
/// subnet's natural hero — this is where admins go to see "what's
/// actually allocated from this /24?" without CSV-exporting the
/// whole pool.
@Component({
  selector: 'app-network-subnet-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/subnets" class="back-link">← Subnets</a>
      <h2 *ngIf="subnet">{{ subnet.subnetCode }} · {{ subnet.network }}</h2>
      <h2 *ngIf="!subnet">Loading…</h2>
      <small *ngIf="subnet" class="subtitle">
        net.subnet · {{ subnet.poolCode ?? '(no pool)' }} · {{ subnet.scopeLevel }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && subnet" class="meta-grid">
      <div class="meta-row"><label>Subnet code</label>  <span>{{ subnet.subnetCode }}</span></div>
      <div class="meta-row"><label>Display name</label> <span>{{ subnet.displayName }}</span></div>
      <div class="meta-row"><label>CIDR</label>         <code>{{ subnet.network }}</code></div>
      <div class="meta-row"><label>Scope level</label>  <span>{{ subnet.scopeLevel }}</span></div>
      <div class="meta-row"><label>Pool</label>
        <span *ngIf="!subnet.poolId">{{ subnet.poolCode ?? '—' }}</span>
        <a *ngIf="subnet.poolId" [routerLink]="['/network/ip-pool', subnet.poolId]"
           class="drill">{{ subnet.poolCode ?? subnet.poolId }}</a>
      </div>
      <div class="meta-row"><label>Linked VLAN</label>  <span>{{ subnet.vlanTag ?? '—' }}</span></div>
      <div class="meta-row"><label>Status</label>       <span>{{ subnet.status }}</span></div>
      <div class="meta-row"><label>Version</label>      <span>{{ subnet.version }}</span></div>
      <div class="meta-row full"><label>UUID</label>    <code>{{ subnet.id }}</code></div>
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

    <!-- Addresses tab -->
    <div *ngIf="activeTab === 2">
      <dx-data-grid [dataSource]="addresses" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     [headerFilter]="{ visible: true }"
                     [groupPanel]="{ visible: true }">
        <dxi-column dataField="address"        caption="Address"       width="160"
                    sortOrder="asc" [sortIndex]="0" [fixed]="true" />
        <dxi-column dataField="assignedToType" caption="Assigned to"   width="140" [groupIndex]="0" />
        <dxi-column dataField="assignedToId"   caption="Entity id"     width="260" />
        <dxi-column dataField="isReserved"     caption="Reserved"      width="100" dataType="boolean" />
        <dxi-column dataField="status"         caption="Status"        width="90" />
        <dxi-column dataField="version"        caption="v"             width="50" />
        <dxi-column dataField="id"             caption="UUID"          width="260" />
      </dx-data-grid>
      <div *ngIf="addresses.length === 0 && !loadingAddresses" class="empty-note">
        No IP addresses allocated from this subnet yet.
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
    .meta-row .drill { color: #3b82f6; text-decoration: none; }
    .meta-row .drill:hover { text-decoration: underline; }
    .empty-note { margin-top: 12px; padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkSubnetDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }, { text: 'Addresses' }];
  activeTab = 0;

  subnetId = '';
  subnet: SubnetListRow | null = null;
  audit: AuditRow[] = [];
  addresses: IpAddressListRow[] = [];
  loadingAddresses = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.subnetId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.subnetId) {
      this.status = 'Missing route param — expected /network/net-subnet/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listSubnets(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.subnet = rows.find(r => r.id === this.subnetId) ?? null;
        this.status = this.subnet ? '' : 'Subnet not found.';
        if (this.subnet) this.loadTabData();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'Subnet', this.subnetId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
    if (this.activeTab === 2 && this.addresses.length === 0 && !this.loadingAddresses) {
      this.loadingAddresses = true;
      this.engine.listIpAddresses(environment.defaultTenantId, this.subnetId).subscribe({
        next: (rows) => { this.addresses = rows; this.loadingAddresses = false; },
        error: () => { this.loadingAddresses = false; },
      });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }
}
