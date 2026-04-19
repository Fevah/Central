import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, ReservationShelfListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide reservation shelf grid. Rows tinted amber when
/// availableAfter is in the past — the cooldown has elapsed but
/// nothing promoted the resource yet, flagging a background-job
/// issue (+ the validation rule `reservation_shelf.cooldown_respected`
/// reports the same state).
@Component({
  selector: 'app-network-reservation-shelf',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Reservation shelf</h2>
      <small class="subtitle">Retired allocations in cooldown before recycling. Amber rows are past their cooldown window — check the recycler job.</small>
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
                   [onCellPrepared]="onCellPrepared">
      <dxi-column dataField="resourceType"   caption="Type"         width="120" [groupIndex]="0" />
      <dxi-column dataField="resourceKey"    caption="Resource key" width="200" sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="retiredAt"      caption="Retired at"   width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="availableAfter" caption="Available after" width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="retiredReason"  caption="Reason" />
      <dxi-column dataField="status"         caption="Status"       width="90" />
      <dxi-column dataField="version"        caption="v"            width="50"  dataType="number" />
      <dxi-column dataField="id"             caption="UUID" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    ::ng-deep .past-cooldown-row { background: rgba(234,179,8,0.08) !important; }
  `]
})
export class NetworkReservationShelfComponent implements OnInit {
  rows: ReservationShelfListRow[] = [];
  loading = false;
  status = '';

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listReservationShelf(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} entr${rows.length === 1 ? 'y' : 'ies'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Amber-tint rows where availableAfter is in the past. Parses
  /// the ISO string each row — cheap enough for a 5000-row grid
  /// without caching, and avoids a wire-shape change.
  onCellPrepared = (e: { rowType?: string; data?: ReservationShelfListRow; cellElement?: HTMLElement }): void => {
    if (e.rowType !== 'data' || !e.data || !e.cellElement) return;
    try {
      if (new Date(e.data.availableAfter).getTime() < Date.now()) {
        e.cellElement.classList.add('past-cooldown-row');
      }
    } catch {
      // Unparseable timestamp — leave alone.
    }
  };
}
