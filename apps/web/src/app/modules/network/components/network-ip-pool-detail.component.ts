import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule, DxTabsModule } from 'devextreme-angular';
import {
  NetworkingEngineService, IpPoolRow, SubnetListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Detail page for one IP pool — closes the last gap on the pool-
/// family entity-detail chain. Summary card (pool CIDR + family +
/// status) + Subnets tab listing every subnet carved from this pool
/// via the new poolId narrower on /api/net/subnets. Carver preview
/// button drills to /network/carver-preview with the pool id
/// pre-selected so operators can immediately dry-run a new subnet
/// without re-typing the pool.
@Component({
  selector: 'app-network-ip-pool-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div *ngIf="!pool && !status" class="loading">Loading…</div>
    <div *ngIf="status" class="status-line" [class.error]="error">{{ status }}</div>

    <ng-container *ngIf="pool">
      <div class="page-header">
        <h2>{{ pool.PoolCode }}</h2>
        <small class="subtitle">
          <span class="mono">{{ pool.PoolCidr }}</span>
          <span class="family">{{ pool.AddressFamily }}</span>
          <span [ngClass]="'status-' + pool.Status.toLowerCase()">{{ pool.Status }}</span>
        </small>
      </div>

      <div class="toolbar">
        <dx-button text="Preview next subnet" icon="plus" type="default"
                   (onClick)="openCarverPreview()" />
        <dx-button text="Pool utilization" icon="chart" stylingMode="text"
                   routerLink="/network/pool-utilization" />
        <dx-button text="Refresh" icon="refresh" stylingMode="text"
                   (onClick)="reload()" [disabled]="loading" />
      </div>

      <dx-tabs [items]="tabs"
               [selectedIndex]="selectedTab"
               (onItemClick)="onTabClick($event)" />

      <ng-container *ngIf="selectedTab === 0">
        <div class="summary-grid">
          <div class="summary-row">
            <label>Pool code</label>
            <span>{{ pool.PoolCode }}</span>
            <label>Display name</label>
            <span>{{ pool.DisplayName }}</span>
          </div>
          <div class="summary-row">
            <label>Pool CIDR</label>
            <span class="mono">{{ pool.PoolCidr }}</span>
            <label>Address family</label>
            <span>{{ pool.AddressFamily }}</span>
          </div>
          <div class="summary-row">
            <label>Status</label>
            <span [ngClass]="'status-' + pool.Status.toLowerCase()">{{ pool.Status }}</span>
            <label>Subnet count</label>
            <span>{{ subnets.length }}</span>
          </div>
        </div>
      </ng-container>

      <ng-container *ngIf="selectedTab === 1">
        <dx-data-grid [dataSource]="subnets" [showBorders]="true" [hoverStateEnabled]="true"
                       [columnAutoWidth]="true"
                       [searchPanel]="{ visible: true }"
                       [filterRow]="{ visible: true }"
                       [headerFilter]="{ visible: true }"
                       (onRowDblClick)="onSubnetDoubleClick($event)">
          <dxi-column dataField="subnetCode"   caption="Code"          [fixed]="true" width="180"
                      sortOrder="asc" />
          <dxi-column dataField="network"      caption="CIDR"          width="200" />
          <dxi-column dataField="displayName"  caption="Display name"  width="220" />
          <dxi-column dataField="scopeLevel"   caption="Scope"         width="110" />
          <dxi-column dataField="vlanTag"      caption="VLAN"          width="80"  dataType="number" />
          <dxi-column dataField="status"       caption="Status"        width="100" />
          <dxi-column dataField="id"           caption="UUID" />
        </dx-data-grid>
      </ng-container>
    </ng-container>
  `,
  styles: [`
    :host { display: block; padding: 12px 16px; }
    .loading, .status-line { color: #888; padding: 24px 0; }
    .status-line.error { color: #cf222e; font-weight: 600; }

    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { display: inline-flex; gap: 10px; align-items: center; color: #888; }
    .mono { font-family: ui-monospace, Menlo, Consolas, monospace; }
    .family {
      padding: 2px 8px; border-radius: 10px;
      background: #ddf4ff; color: #0969da; font-size: 11px;
    }
    .status-active        { color: #1a7f37; font-weight: 600; }
    .status-planned       { color: #0969da; }
    .status-decommissioned { color: #8b949e; }

    .toolbar { display: flex; gap: 10px; align-items: center; margin: 12px 0; }

    .summary-grid { margin-top: 16px; display: flex; flex-direction: column; gap: 6px; }
    .summary-row  { display: grid; grid-template-columns: 120px 1fr 120px 1fr; gap: 8px; align-items: baseline; }
    .summary-row label { color: #57606a; font-size: 12px; }
  `],
})
export class NetworkIpPoolDetailComponent implements OnInit {
  pool: IpPoolRow | null = null;
  subnets: SubnetListRow[] = [];
  loading = false;
  status = '';
  error = false;

  tabs = [{ text: 'Summary' }, { text: 'Subnets' }];
  selectedTab = 0;

  private poolId = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.poolId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.poolId) {
      this.status = 'Missing route param — expected /network/ip-pool/:id.';
      this.error = true;
      return;
    }
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.error = false;
    this.engine.listIpPools().subscribe({
      next: (pools) => {
        this.pool = pools.find(p => p.Id === this.poolId) ?? null;
        if (!this.pool) {
          this.status = 'IP pool not found.';
          this.error = true;
          this.loading = false;
          return;
        }
        this.status = '';
        this.loadSubnets();
      },
      error: (err) => {
        this.status = `Pool load failed: ${err?.message ?? err}`;
        this.error = true;
        this.loading = false;
      },
    });
  }

  private loadSubnets(): void {
    this.engine.listSubnets(environment.defaultTenantId, this.poolId).subscribe({
      next: (rows) => {
        this.subnets = rows ?? [];
        this.loading = false;
      },
      error: (err) => {
        this.status = `Subnet load failed: ${err?.message ?? err}`;
        this.error = true;
        this.loading = false;
        this.subnets = [];
      },
    });
  }

  onTabClick(e: { itemIndex: number }): void {
    this.selectedTab = e.itemIndex ?? 0;
  }

  openCarverPreview(): void {
    this.router.navigate(['/network/carver-preview'],
      { queryParams: { poolId: this.poolId } });
  }

  onSubnetDoubleClick(e: { data: SubnetListRow }): void {
    if (!e?.data?.id) return;
    this.router.navigate(['/network/net-subnet', e.data.id]);
  }
}
