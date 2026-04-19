import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, IpAddressListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide IP address grid — sibling to the per-subnet
/// Addresses tab on the subnet detail page. Reuses the
/// /api/net/ip-addresses thin list without the subnetId narrower,
/// so operators can scan the tenant's entire IPAM in one grid.
///
/// Grouped by subnetCode by default so addresses cluster per
/// subnet. Cap-hit hint surfaces when 5000 rows return.
@Component({
  selector: 'app-network-ip-addresses',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>IP addresses</h2>
      <small class="subtitle">net.ip_address rows across every subnet in the tenant. Capped at 5000 — drill into a subnet for that subnet only.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="subnetCode"     caption="Subnet"   [fixed]="true" width="200" [groupIndex]="0" />
      <dxi-column dataField="address"        caption="Address"  width="160" sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="assignedToType" caption="Assigned" width="140" />
      <dxi-column dataField="assignedToId"   caption="Entity id" width="260" />
      <dxi-column dataField="isReserved"     caption="Reserved" width="100" dataType="boolean" />
      <dxi-column dataField="status"         caption="Status"   width="90" />
      <dxi-column dataField="version"        caption="v"        width="50"  dataType="number" />
      <dxi-column dataField="id"             caption="UUID" />
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
export class NetworkIpAddressesComponent implements OnInit {
  rows: IpAddressListRow[] = [];
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
    this.engine.listIpAddresses(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} address${rows.length === 1 ? '' : 'es'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — drill into a subnet for that subnet only)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Double-click → owning subnet detail page. Addresses tab on
  /// that page shows only this subnet's IPs.
  onRowDoubleClick(e: { data: IpAddressListRow }): void {
    const row = e?.data;
    if (!row?.subnetId) return;
    this.router.navigate(['/network/net-subnet', row.subnetId]);
  }
}
