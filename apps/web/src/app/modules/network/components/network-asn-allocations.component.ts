import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, AsnAllocationListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide ASN allocation grid. Grouped by block code by
/// default so each block's allocations cluster. Double-click
/// drills to the owning Device / Server detail when the target
/// type is Device / Server; other types fall through to audit.
@Component({
  selector: 'app-network-asn-allocations',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>ASN allocations</h2>
      <small class="subtitle">Every assigned ASN across the tenant. Grouped by block; ordered by ASN ascending so gaps are visible.</small>
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
      <dxi-column dataField="blockCode"        caption="Block"           [fixed]="true" width="160"
                  [groupIndex]="0" />
      <dxi-column dataField="asn"              caption="ASN"             width="120" dataType="number"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="allocatedToType"  caption="Target type"     width="130" />
      <dxi-column dataField="targetDisplay"    caption="Target"          width="220" />
      <dxi-column dataField="allocatedToId"    caption="Target id" />
      <dxi-column dataField="status"           caption="Status"          width="90" />
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
export class NetworkAsnAllocationsComponent implements OnInit {
  rows: AsnAllocationListRow[] = [];
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
    this.engine.listAsnAllocations(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} allocation${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  onRowDoubleClick(e: { data: AsnAllocationListRow }): void {
    const row = e?.data;
    if (!row?.allocatedToId) return;
    const route =
      row.allocatedToType === 'Device' ? '/network/net-device'
    : row.allocatedToType === 'Server' ? '/network/net-server'
    :                                     null;
    if (route) {
      this.router.navigate([route, row.allocatedToId]);
      return;
    }
    this.router.navigate(['/network/audit', row.allocatedToType, row.allocatedToId]);
  }
}
