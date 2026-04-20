import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
  DxPopupModule, DxTextBoxModule, DxTextAreaModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ChangeSet, ChangeSetStatusCount,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Read-only web view of net.change_set. Parallel to the WPF
/// ChangesetsPanel (when that lands). Lists every Set in the tenant
/// with an optional status filter; double-click drills to the
/// correlation-scoped audit search so the operator sees every
/// mutation + lifecycle event the Set produced.
///
/// Write flow (submit / approve / apply / cancel / rollback) stays
/// WPF-only for now — the web client needs the matching approval
/// dialog chrome before those actions are safe to expose.
@Component({
  selector: 'app-network-change-sets',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule,
            DxPopupModule, DxTextBoxModule, DxTextAreaModule],
  template: `
    <div class="page-header">
      <h2>Change sets</h2>
      <small class="subtitle">Lifecycle-managed batches of edits. Read-only on the web today; submit / approve / apply run through the WPF client until the web approval chrome lands.</small>
    </div>

    <!-- Status summary banner — one pill per status with its count.
         Click a pill to filter the grid to just that status. Always
         renders all seven state-machine positions in order so
         operators can spot empty buckets (no Approved waiting to
         apply / no Draft queue / etc.) instantly. -->
    <div class="status-banner" *ngIf="summary.length">
      <a *ngFor="let s of summary"
         class="status-pill"
         [class.active]="statusFilter === s.status"
         [ngClass]="'pill-' + s.status.toLowerCase()"
         href="javascript:void(0)"
         (click)="filterByStatus(s.status)">
        <span class="pill-label">{{ s.status }}</span>
        <span class="pill-count">{{ s.count }}</span>
      </a>
      <a class="status-pill pill-clear" *ngIf="statusFilter"
         href="javascript:void(0)" (click)="filterByStatus(null)">
        Clear filter ×
      </a>
    </div>

    <div class="filter-bar">
      <label>Status</label>
      <dx-select-box class="md" [items]="statuses" [(value)]="statusFilter"
                     [showClearButton]="true" placeholder="(all)"
                     (onValueChanged)="reload()" />

      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>

      <span class="spacer"></span>

      <dx-button text="New change set" icon="add" type="default"
                 stylingMode="contained" (onClick)="openCreateDialog()" />
    </div>

    <!-- Create dialog — builds a Draft set with no items. Items
         are added separately (WPF only for now); this surface
         gets the Set created so operators can see it in Draft
         state + populate items via the engine's /items endpoint
         later. -->
    <dx-popup [(visible)]="createDialogOpen"
              [width]="520" [height]="420"
              title="New change set"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>Title *</label>
          <dx-text-box [(value)]="createDraft.title"
                       placeholder="e.g. Immunocore Q2 building-17 core-swap" />
        </div>
        <div class="form-row">
          <label>Description</label>
          <dx-text-area [(value)]="createDraft.description" [height]="80"
                        placeholder="Optional — goes into the Set's meta row + audit." />
        </div>
        <div class="form-row">
          <label>Requested by (display)</label>
          <dx-text-box [(value)]="createDraft.requestedByDisplay"
                       placeholder="Optional — name to attribute the Set to" />
          <small class="hint">Independent of the authenticated user id, which the engine pulls from the X-User-Id header.</small>
        </div>
        <div *ngIf="formError" class="form-error">{{ formError }}</div>
        <div class="form-actions">
          <dx-button text="Cancel" (onClick)="closeCreateDialog()" />
          <dx-button text="Create" type="default" (onClick)="submitCreate()"
                     [disabled]="busy" />
        </div>
      </div>
    </dx-popup>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="title" caption="Title" [fixed]="true" width="260"
                  cellTemplate="titleTemplate" />
      <dxi-column dataField="status"             caption="Status"              width="120" [groupIndex]="0"
                  cellTemplate="statusTemplate" />
      <dxi-column dataField="itemCount"          caption="Items"               width="80"  dataType="number" />
      <dxi-column dataField="requestedByDisplay" caption="Requested by"        width="160" />
      <dxi-column dataField="requiredApprovals"  caption="Approvals needed"    width="150" dataType="number" />
      <dxi-column dataField="submittedAt"        caption="Submitted"           width="160" dataType="datetime"
                  format="yyyy-MM-dd HH:mm" />
      <dxi-column dataField="approvedAt"         caption="Approved"            width="160" dataType="datetime"
                  format="yyyy-MM-dd HH:mm" />
      <dxi-column dataField="appliedAt"          caption="Applied"             width="160" dataType="datetime"
                  format="yyyy-MM-dd HH:mm" />
      <dxi-column dataField="updatedAt"          caption="Last updated"        width="160" dataType="datetime"
                  format="yyyy-MM-dd HH:mm" [sortIndex]="0" sortOrder="desc" />
      <dxi-column dataField="correlationId"      caption="Correlation"         width="240"
                  cellTemplate="correlationTemplate" />
      <dxi-column dataField="id"                 caption="Set id"              width="260" />

      <div *dxTemplate="let d of 'titleTemplate'">
        <a [routerLink]="['/network/change-sets', d.data.id]"
           class="title-link" (click)="$event.stopPropagation()">{{ d.value }}</a>
      </div>
      <div *dxTemplate="let d of 'statusTemplate'">
        <span [class]="'badge badge-' + statusBadgeClass(d.value)">{{ d.value }}</span>
      </div>
      <div *dxTemplate="let d of 'correlationTemplate'">
        <a href="javascript:void(0)" class="correlation-link"
           title="Show audit rows for this change set"
           (click)="drillCorrelation($event, d.value)">{{ d.value }}</a>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .filter-bar { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .md { width: 180px; }
    .status-line { color: #666; font-size: 12px; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-draft      { background: rgba(148,163,184,0.2); color: #cbd5e1; }
    .badge-submitted  { background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .badge-approved   { background: rgba(168,85,247,0.2);  color: #a855f7; }
    .badge-rejected   { background: rgba(239,68,68,0.2);   color: #ef4444; }
    .badge-applied    { background: rgba(34,197,94,0.2);   color: #22c55e; }
    .badge-rolledback { background: rgba(234,179,8,0.2);   color: #eab308; }
    .badge-cancelled  { background: rgba(107,114,128,0.2); color: #9ca3af; }
    .correlation-link { color: #60a5fa; font-family: ui-monospace, monospace; font-size: 11px; text-decoration: none; }
    .correlation-link:hover { text-decoration: underline; }
    .title-link { color: #60a5fa; text-decoration: none; font-weight: 600; }
    .title-link:hover { text-decoration: underline; }
    .filter-bar .spacer { flex: 1; min-width: 12px; }

    .status-banner {
      display: flex; gap: 6px; flex-wrap: wrap; margin: 10px 0 14px;
    }
    .status-pill {
      display: inline-flex; align-items: center; gap: 6px;
      padding: 4px 10px; border-radius: 14px; font-size: 11px; font-weight: 600;
      text-decoration: none; cursor: pointer; color: inherit;
      border: 1px solid transparent; transition: filter 0.15s;
    }
    .status-pill:hover  { filter: brightness(1.15); }
    .status-pill.active { border-color: currentColor; box-shadow: 0 0 0 1px currentColor; }
    .status-pill .pill-count {
      background: rgba(0,0,0,0.25); padding: 0 6px; border-radius: 8px;
      font-size: 10px;
    }
    .pill-draft      { background: rgba(148,163,184,0.2); color: #cbd5e1; }
    .pill-submitted  { background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .pill-approved   { background: rgba(168,85,247,0.2);  color: #a855f7; }
    .pill-rejected   { background: rgba(239,68,68,0.2);   color: #ef4444; }
    .pill-applied    { background: rgba(34,197,94,0.2);   color: #22c55e; }
    .pill-rolledback { background: rgba(234,179,8,0.2);   color: #eab308; }
    .pill-cancelled  { background: rgba(107,114,128,0.2); color: #9ca3af; }
    .pill-clear      { background: transparent; color: #64748b; font-weight: 400; }
    .form { display: flex; flex-direction: column; gap: 14px; padding: 4px 2px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .form-row .hint { color: #6b7280; font-size: 11px; }
    .form-error { color: #ef4444; font-size: 12px; padding: 6px 8px; background: rgba(239,68,68,0.08); border-radius: 4px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
  `]
})
export class NetworkChangeSetsComponent implements OnInit {
  /// Statuses mirror `ChangeSetStatus` in the engine. The engine
  /// filter is case-sensitive so these PascalCase strings round-trip.
  statuses = ['Draft', 'Submitted', 'Approved', 'Rejected',
              'Applied', 'RolledBack', 'Cancelled'];

  statusFilter: string | null = null;

  /// Per-status rollup from /api/net/change-sets/summary — drives
  /// the top-of-page status banner. Always 7 rows in state-machine
  /// order, zero-count buckets included so the banner layout is
  /// consistent regardless of activity.
  summary: ChangeSetStatusCount[] = [];

  createDialogOpen = false;
  busy = false;
  formError = '';
  createDraft: {
    title: string;
    description: string;
    requestedByDisplay: string;
  } = { title: '', description: '', requestedByDisplay: '' };
  rows: ChangeSet[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.reload();
    this.loadSummary();
  }

  /// Summary rollup — independent of the grid reload so switching
  /// filters doesn't re-fetch the counts.
  private loadSummary(): void {
    this.engine.changeSetStatusSummary(environment.defaultTenantId).subscribe({
      next: (rows) => { this.summary = rows ?? []; },
      error: () => { /* silent — banner just won't render */ },
    });
  }

  /// Click a status pill → apply as filter + reload grid. Passing
  /// null clears the filter (same behaviour as the Clear pill).
  filterByStatus(s: string | null): void {
    this.statusFilter = s;
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listChangeSets(
      environment.defaultTenantId,
      this.statusFilter ?? undefined,
    ).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} change set${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Lowercase for CSS selector match with the .badge-<status> rules.
  statusBadgeClass(status: string): string {
    return (status || 'draft').toLowerCase();
  }

  /// Correlation-link click → audit search scoped to that correlation.
  /// Stop propagation so the row's double-click handler isn't also
  /// activated (single click shouldn't swap to a row-level drill).
  drillCorrelation(evt: Event, correlationId: string): void {
    evt?.stopPropagation?.();
    if (!correlationId) return;
    this.router.navigate(['/network/audit-search'], {
      queryParams: { correlationId },
    });
  }

  /// Double-click the row → audit-search for the set's correlation.
  /// Same drill target as the correlation link for consistency with
  /// the other grids' "double-click to see history" UX.
  onRowDoubleClick(e: { data: ChangeSet }): void {
    const c = e?.data?.correlationId;
    if (!c) return;
    this.router.navigate(['/network/audit-search'], {
      queryParams: { correlationId: c },
    });
  }

  openCreateDialog(): void {
    this.createDraft = { title: '', description: '', requestedByDisplay: '' };
    this.formError = '';
    this.createDialogOpen = true;
  }

  closeCreateDialog(): void {
    this.createDialogOpen = false;
    this.formError = '';
  }

  submitCreate(): void {
    const d = this.createDraft;
    this.formError = '';
    if (!d.title.trim()) { this.formError = 'Title is required.'; return; }

    this.busy = true;
    this.engine.createChangeSet({
      organizationId:     environment.defaultTenantId,
      title:              d.title.trim(),
      description:        d.description.trim() || null,
      requestedByDisplay: d.requestedByDisplay.trim() || null,
    }).subscribe({
      next: (created) => {
        this.busy = false;
        this.createDialogOpen = false;
        this.status = `Change set created — id ${created.id}.`;
        // Drill straight to the new Set's detail page so the
        // operator can continue adding items / submit. Reload
        // first happens implicitly when they come back.
        this.router.navigate(['/network/change-sets', created.id]);
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.formError = 'Forbidden — your user lacks write:ChangeSet.';
        } else {
          this.formError = err?.error?.detail ?? err?.message ?? 'Create failed.';
        }
      },
    });
  }
}
