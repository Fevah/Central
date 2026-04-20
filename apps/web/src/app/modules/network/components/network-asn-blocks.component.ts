import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, AsnBlockListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide ASN block grid with per-block availability.
/// Same shape as /network/vlan-blocks — amber rows near exhaustion
/// so operators spot near-full blocks.
@Component({
  selector: 'app-network-asn-blocks',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>ASN blocks</h2>
      <small class="subtitle">Per-block ASN availability ({{ totalAvailable.toLocaleString() }} / {{ totalCapacity.toLocaleString() }} ASNs free). Rows tint amber when less than 10% remains.</small>
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
                   (onCellPrepared)="onCellPrepared($event)">
      <dxi-column dataField="blockCode"   caption="Block code"   [fixed]="true" width="160"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="displayName" caption="Display name" width="260" />
      <dxi-column dataField="asnFirst"    caption="First ASN"    width="130" dataType="number" />
      <dxi-column dataField="asnLast"     caption="Last ASN"     width="130" dataType="number" />
      <dxi-column dataField="available"   caption="Available"    width="130" dataType="number" />
      <dxi-column dataField="id"          caption="UUID" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    ::ng-deep .near-full-row { background: rgba(234,179,8,0.08) !important; }
  `]
})
export class NetworkAsnBlocksComponent implements OnInit {
  rows: AsnBlockListRow[] = [];
  loading = false;
  status = '';

  get totalAvailable(): number { return this.rows.reduce((n, r) => n + r.available, 0); }
  get totalCapacity(): number {
    return this.rows.reduce((n, r) => n + (r.asnLast - r.asnFirst + 1), 0);
  }

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listAsnBlockAvailability(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} block${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  onCellPrepared = (e: { rowType?: string; data?: AsnBlockListRow; cellElement?: HTMLElement }): void => {
    if (e.rowType !== 'data' || !e.data || !e.cellElement) return;
    const capacity = e.data.asnLast - e.data.asnFirst + 1;
    if (capacity > 0 && e.data.available * 10 < capacity) {
      e.cellElement.classList.add('near-full-row');
    }
  };
}
