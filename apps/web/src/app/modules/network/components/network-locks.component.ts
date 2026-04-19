import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, LockedRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web view of the Phase-5 lock state across the five numbering
/// tables (asn_allocation / vlan / mlag_domain / subnet /
/// ip_address). Read-only for this slice; the WPF LocksPanel retains
/// the SetLock PATCH write path until the web approval chrome lands
/// for the HardLock → Immutable transition case.
///
/// A uniform row shape across five source tables means one grid can
/// render them all — the `tableName` column disambiguates. State
/// badges use the same colour language as the WPF panel (HardLock
/// red, Immutable purple, SoftLock amber).
@Component({
  selector: 'app-network-locks',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>Locks</h2>
      <small class="subtitle">Every non-Open lock across the Phase-5 numbering tables. Read-only; the WPF LocksPanel has the PATCH write path.</small>
    </div>

    <div class="filter-bar">
      <label>Table</label>
      <dx-select-box class="md" [items]="tables" [(value)]="tableFilter"
                     [showClearButton]="true" placeholder="(all)"
                     (onValueChanged)="reload()" />

      <label>State</label>
      <dx-select-box class="sm" [items]="states" [(value)]="stateFilter"
                     [showClearButton]="true" placeholder="(any non-Open)"
                     (onValueChanged)="reload()" />

      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }">
      <dxi-column dataField="tableName"    caption="Table"   width="140" [groupIndex]="0" />
      <dxi-column dataField="displayLabel" caption="Row"     [fixed]="true" width="260" sortOrder="asc" />
      <dxi-column dataField="lockState"    caption="State"   width="120" cellTemplate="stateTemplate" />
      <dxi-column dataField="lockReason"   caption="Reason" />
      <dxi-column dataField="lockedBy"     caption="User id" width="80"  dataType="number" />
      <dxi-column dataField="lockedAt"     caption="Locked at" width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="version"      caption="v"       width="60"  dataType="number" />
      <dxi-column dataField="id"           caption="Row id"  width="260" />

      <div *dxTemplate="let d of 'stateTemplate'">
        <span [class]="'badge badge-' + (d.value || '').toLowerCase()">{{ d.value }}</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .filter-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 10px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .sm { width: 140px; }
    .filter-bar .md { width: 200px; }
    .status-line { color: #666; font-size: 12px; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-hardlock  { background: rgba(239,68,68,0.2);  color: #ef4444; }
    .badge-immutable { background: rgba(168,85,247,0.2); color: #a855f7; }
    .badge-softlock  { background: rgba(234,179,8,0.2);  color: #eab308; }
    .badge-open      { background: rgba(107,114,128,0.2); color: #9ca3af; }
  `]
})
export class NetworkLocksComponent implements OnInit {
  /// Lock enforcement covers these five tables (see locks.rs). Web
  /// filter uses the engine's wire values verbatim (snake_case).
  tables = ['asn_allocation', 'vlan', 'mlag_domain', 'subnet', 'ip_address'];
  states = ['HardLock', 'Immutable', 'SoftLock'];

  tableFilter: string | null = null;
  stateFilter: string | null = null;
  rows: LockedRow[] = [];
  loading = false;
  status = '';

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listLockedRows(
      environment.defaultTenantId,
      this.tableFilter ?? undefined,
      this.stateFilter ?? undefined,
    ).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} locked row${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }
}
