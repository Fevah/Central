import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, IpAddressListRow, AuditRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// net.ip_address detail page — seventh entity detail. Two tabs:
/// Summary (row + subnet link) + Audit (last 100 entries).
///
/// Routed at /network/ip-address/:id. Drilled into from the
/// subnet-detail Addresses tab (eventually — currently that tab
/// doesn't drill further; this detail page is the landing target).
@Component({
  selector: 'app-network-ip-address-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/ip-addresses" class="back-link">← IP addresses</a>
      <h2 *ngIf="ip">{{ ip.address }}</h2>
      <h2 *ngIf="!ip">Loading…</h2>
      <small *ngIf="ip" class="subtitle">
        net.ip_address · in {{ ip.subnetCode ?? '(unknown subnet)' }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             (onItemClick)="onTabChanged($event)"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && ip" class="meta-grid">
      <div class="meta-row"><label>Address</label>      <code>{{ ip.address }}</code></div>
      <div class="meta-row">
        <label>Subnet</label>
        <a *ngIf="ip.subnetId" href="javascript:void(0)"
           class="subnet-link" (click)="drillSubnet()">
          {{ ip.subnetCode ?? ip.subnetId }}
        </a>
      </div>
      <div class="meta-row"><label>Assigned to type</label><span>{{ ip.assignedToType ?? '—' }}</span></div>
      <div class="meta-row"><label>Assigned to id</label>  <code>{{ ip.assignedToId ?? '—' }}</code></div>
      <div class="meta-row"><label>Reserved</label>       <span>{{ ip.isReserved }}</span></div>
      <div class="meta-row"><label>Status</label>         <span>{{ ip.status }}</span></div>
      <div class="meta-row"><label>Version</label>        <span>{{ ip.version }}</span></div>
      <div class="meta-row full"><label>UUID</label>      <code>{{ ip.id }}</code></div>
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
    .subnet-link { color: #60a5fa; text-decoration: none; }
    .subnet-link:hover { text-decoration: underline; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkIpAddressDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Audit' }];
  activeTab = 0;

  ipId = '';
  ip: IpAddressListRow | null = null;
  audit: AuditRow[] = [];
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.ipId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.ipId) {
      this.status = 'Missing route param — expected /network/ip-address/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listIpAddresses(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.ip = rows.find(r => r.id === this.ipId) ?? null;
        this.status = this.ip ? '' : 'IP address not found.';
        if (this.ip) this.loadTabData();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  loadTabData(): void {
    if (this.activeTab === 1 && this.audit.length === 0) {
      this.engine.getEntityTimeline(environment.defaultTenantId, 'IpAddress', this.ipId, 100)
        .subscribe({ next: (rows) => { this.audit = rows; }, error: () => {} });
    }
  }

  onTabChanged(_e: unknown): void { this.loadTabData(); }

  drillSubnet(): void {
    if (!this.ip?.subnetId) return;
    this.router.navigate(['/network/net-subnet', this.ip.subnetId]);
  }
}
