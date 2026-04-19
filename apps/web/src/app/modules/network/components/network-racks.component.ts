import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, RackListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide racks grid. Sibling to rooms grid. Double-click
/// drills to /network/rack/:id.
@Component({
  selector: 'app-network-racks',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Racks</h2>
      <small class="subtitle">net.rack rows across the tenant.</small>
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
      <dxi-column dataField="rackCode"   caption="Rack code" [fixed]="true" width="160"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="row"        caption="Row"       width="80" />
      <dxi-column dataField="position"   caption="Position"  width="100" dataType="number" />
      <dxi-column dataField="uHeight"    caption="U height"  width="100" dataType="number" />
      <dxi-column dataField="maxDevices" caption="Max devs"  width="110" dataType="number" />
      <dxi-column dataField="roomId"     caption="Room id" />
      <dxi-column dataField="status"     caption="Status"    width="90" />
      <dxi-column dataField="id"         caption="UUID" />
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
export class NetworkRacksComponent implements OnInit {
  rows: RackListRow[] = [];
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
    this.engine.listRacks(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} rack${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  onRowDoubleClick(e: { data: RackListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/rack', row.id]);
  }
}
