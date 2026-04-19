import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, RoomListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide rooms grid. Sibling to ports + racks + aggregate-
/// ethernet + MLAG + modules grids. Grouped by roomType by default
/// so DataHall / MDF / IDF cohorts cluster.
@Component({
  selector: 'app-network-rooms',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Rooms</h2>
      <small class="subtitle">net.room rows across the tenant. Double-click to drill to a room's racks.</small>
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
      <dxi-column dataField="roomCode" caption="Room code" [fixed]="true" width="160"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="roomType" caption="Type"      width="120" [groupIndex]="0" />
      <dxi-column dataField="maxRacks" caption="Max racks" width="110" dataType="number" />
      <dxi-column dataField="floorId"  caption="Floor id" />
      <dxi-column dataField="status"   caption="Status"   width="90" />
      <dxi-column dataField="id"       caption="UUID" />
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
export class NetworkRoomsComponent implements OnInit {
  rows: RoomListRow[] = [];
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
    this.engine.listRooms(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} room${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  onRowDoubleClick(e: { data: RoomListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/room', row.id]);
  }
}
