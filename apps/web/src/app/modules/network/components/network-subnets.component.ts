import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, SubnetListRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF SubnetGridPanel. Backed by the engine's
/// /api/net/subnets thin list. network is pre-rendered as CIDR text so
/// there's nothing to parse client-side; scope level groups naturally
/// via the header filter (Free / Region / Site / Building / Floor /
/// Room). Double-click drills to audit timeline for the subnet uuid.
@Component({
  selector: 'app-network-subnets',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Subnets</h2>
      <small class="subtitle">net.subnet rows with no-overlap GIST constraint. Pool + linked VLAN tag resolved at list time.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="subnets" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="subnetCode"  caption="Subnet code" [fixed]="true" width="180" sortOrder="asc" />
      <dxi-column dataField="displayName" caption="Name"        width="220" />
      <dxi-column dataField="network"     caption="CIDR"        width="160" />
      <dxi-column dataField="scopeLevel"  caption="Scope"       width="110" />
      <dxi-column dataField="poolCode"    caption="Pool"        width="120" />
      <dxi-column dataField="vlanTag"     caption="VLAN"        width="80"  dataType="number" />
      <dxi-column dataField="status"      caption="Status"      width="90" />
      <dxi-column dataField="version"     caption="v"           width="50"  dataType="number" />
      <dxi-column dataField="id"          caption="UUID"        width="260" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
  `]
})
export class NetworkSubnetsComponent implements OnInit {
  subnets: SubnetListRow[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listSubnets(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.subnets = rows;
        this.loading = false;
        this.status = `${rows.length} subnet${rows.length === 1 ? '' : 's'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — use Search for narrowing)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.subnets = [];
      },
    });
  }

  /// Double-click → subnet detail (Summary / Audit / Addresses).
  onRowDoubleClick(e: { data: SubnetListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/net-subnet', row.id]);
  }
}
