import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, AggregateEthernetListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide aggregate_ethernet grid — sibling to the ports
/// tenant page. Member count comes from the engine's correlated
/// subquery; grid highlights bundles where memberCount <
/// minLinks (amber) so operators can spot under-populated bundles
/// at a glance.
@Component({
  selector: 'app-network-aggregate-ethernet',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Aggregate ethernet</h2>
      <small class="subtitle">LACP bundles across every device in the tenant. Rows highlight when member count drops below min_links.</small>
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
                   (onRowDblClick)="onRowDoubleClick($event)"
                   [onCellPrepared]="onCellPrepared">
      <dxi-column dataField="deviceHostname" caption="Device"   [fixed]="true" width="180"
                  [groupIndex]="0" />
      <dxi-column dataField="aeName"         caption="AE name"  width="100" sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="lacpMode"       caption="LACP"     width="90" />
      <dxi-column dataField="minLinks"       caption="Min links" width="100" dataType="number" />
      <dxi-column dataField="memberCount"    caption="Members"   width="100" dataType="number" />
      <dxi-column dataField="status"         caption="Status"    width="90" />
      <dxi-column dataField="description"    caption="Description" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    ::ng-deep .underpopulated-row { background: rgba(234,179,8,0.08) !important; }
  `]
})
export class NetworkAggregateEthernetComponent implements OnInit {
  rows: AggregateEthernetListRow[] = [];
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
    this.engine.listAggregateEthernet(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} bundle${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Amber-tint rows where memberCount < minLinks — operators scan
  /// for this when debugging half-dead LACP bundles. Bound via
  /// [onCellPrepared] because row-level CSS classes are a DX
  /// per-cell API, not a top-level grid option.
  onCellPrepared = (e: { rowType?: string; data?: AggregateEthernetListRow; cellElement?: HTMLElement }): void => {
    if (e.rowType !== 'data' || !e.data || !e.cellElement) return;
    if (e.data.memberCount < e.data.minLinks) {
      e.cellElement.classList.add('underpopulated-row');
    }
  };

  onRowDoubleClick(e: { data: AggregateEthernetListRow }): void {
    const row = e?.data;
    if (!row?.deviceId) return;
    this.router.navigate(['/network/net-device', row.deviceId]);
  }
}
