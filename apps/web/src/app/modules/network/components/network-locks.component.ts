import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
  DxPopupModule, DxTextAreaModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, LockedRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web view + write path for the Phase-5 lock state across the five
/// numbering tables (asn_allocation / vlan / mlag_domain / subnet /
/// ip_address). PATCH sends transitions through the same server-side
/// guard the WPF LocksPanel uses; Immutable is terminal + can't be
/// loosened (server returns 400).
///
/// A uniform row shape across five source tables means one grid can
/// render them all — the `tableName` column disambiguates. State
/// badges use the same colour language as the WPF panel (HardLock
/// red, Immutable purple, SoftLock amber).
@Component({
  selector: 'app-network-locks',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule,
            DxPopupModule, DxTextAreaModule],
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
      <dxi-column caption="" width="100" [allowFiltering]="false" [allowSorting]="false"
                  cellTemplate="actionsTemplate" />

      <div *dxTemplate="let d of 'stateTemplate'">
        <span [class]="'badge badge-' + (d.value || '').toLowerCase()">{{ d.value }}</span>
      </div>
      <div *dxTemplate="let d of 'actionsTemplate'">
        <div class="row-actions">
          <dx-button icon="edit" stylingMode="text" hint="Change lock state"
                     (onClick)="openSetLock(d.data)" />
        </div>
      </div>
    </dx-data-grid>

    <!-- Set Lock dialog — PATCH the transition. Immutable is
         terminal, so once you flip a row to Immutable you can't
         loosen it through this UI (or anywhere else — the server
         rejects the transition). -->
    <dx-popup [(visible)]="setLockDialogOpen"
              [width]="480" [height]="440"
              title="Change lock state"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>Target</label>
          <span class="readonly">{{ setLockTarget?.tableName }} · {{ setLockTarget?.displayLabel }}</span>
        </div>
        <div class="form-row">
          <label>Current state</label>
          <span [class]="'badge badge-' + (setLockTarget?.lockState || '').toLowerCase()">{{ setLockTarget?.lockState }}</span>
        </div>
        <div class="form-row">
          <label>New state *</label>
          <dx-select-box [items]="allStates" [(value)]="setLockDraft.lockState" />
          <small *ngIf="setLockDraft.lockState === 'Immutable'" class="hint warn">
            Immutable is terminal — can't be loosened once set.
          </small>
        </div>
        <div class="form-row">
          <label>Reason</label>
          <dx-text-area [(value)]="setLockDraft.lockReason" [height]="70"
                        placeholder="Optional — goes into the audit row." />
        </div>
        <div *ngIf="formError" class="form-error">{{ formError }}</div>
        <div class="form-actions">
          <dx-button text="Cancel" (onClick)="closeSetLock()" />
          <dx-button text="Apply" type="default" (onClick)="submitSetLock()"
                     [disabled]="busy" />
        </div>
      </div>
    </dx-popup>
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
    .row-actions { display: flex; gap: 4px; }
    .form { display: flex; flex-direction: column; gap: 14px; padding: 4px 2px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .form-row .hint { color: #6b7280; font-size: 11px; }
    .form-row .hint.warn { color: #a855f7; font-weight: 600; }
    .form-row .readonly { color: #cbd5e1; padding: 6px 8px; background: #0f172a; border-radius: 4px; font-size: 13px; }
    .form-error { color: #ef4444; font-size: 12px; padding: 6px 8px; background: rgba(239,68,68,0.08); border-radius: 4px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
  `]
})
export class NetworkLocksComponent implements OnInit {
  /// Lock enforcement covers these five tables (see locks.rs). Web
  /// filter uses the engine's wire values verbatim (snake_case).
  tables = ['asn_allocation', 'vlan', 'mlag_domain', 'subnet', 'ip_address'];
  states = ['HardLock', 'Immutable', 'SoftLock'];

  /// States accepted by the PATCH body — Open loosens, SoftLock /
  /// HardLock / Immutable tighten. Immutable is terminal.
  allStates = ['Open', 'SoftLock', 'HardLock', 'Immutable'];

  tableFilter: string | null = null;
  stateFilter: string | null = null;
  rows: LockedRow[] = [];
  loading = false;
  busy = false;
  status = '';

  setLockDialogOpen = false;
  setLockTarget: LockedRow | null = null;
  setLockDraft: { lockState: string; lockReason: string } = { lockState: 'Open', lockReason: '' };
  formError = '';

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

  openSetLock(row: LockedRow): void {
    this.setLockTarget = row;
    // Default the new state to Open (the most common "clear a lock"
    // action). Operators flipping to a higher state switch via the
    // combo.
    this.setLockDraft = {
      lockState:  'Open',
      lockReason: row.lockReason ?? '',
    };
    this.formError = '';
    this.setLockDialogOpen = true;
  }

  closeSetLock(): void {
    this.setLockDialogOpen = false;
    this.setLockTarget = null;
    this.formError = '';
  }

  submitSetLock(): void {
    if (!this.setLockTarget) return;
    const d = this.setLockDraft;
    if (!d.lockState) { this.formError = 'New state is required.'; return; }

    this.busy = true;
    this.engine.setEntityLock(
      this.setLockTarget.tableName,
      this.setLockTarget.id,
      environment.defaultTenantId,
      { lockState: d.lockState, lockReason: d.lockReason.trim() || null },
    ).subscribe({
      next: () => {
        this.busy = false;
        this.setLockDialogOpen = false;
        this.setLockTarget = null;
        this.status = `Lock state set to ${d.lockState}.`;
        this.reload();
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 400) {
          // Server rejects illegal transitions (typically Immutable → anything).
          this.formError = err?.error?.detail ??
            'Illegal transition — Immutable is terminal + can\'t be loosened.';
        } else if (status === 403) {
          this.formError = 'Forbidden — your user lacks the lock-admin permission.';
        } else {
          this.formError = err?.error?.detail ?? err?.message ?? 'Set failed.';
        }
      },
    });
  }
}
