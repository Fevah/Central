import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ChangeSet,
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
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>Change sets</h2>
      <small class="subtitle">Lifecycle-managed batches of edits. Read-only on the web today; submit / approve / apply run through the WPF client until the web approval chrome lands.</small>
    </div>

    <div class="filter-bar">
      <label>Status</label>
      <dx-select-box class="md" [items]="statuses" [(value)]="statusFilter"
                     [showClearButton]="true" placeholder="(all)"
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
  `]
})
export class NetworkChangeSetsComponent implements OnInit {
  /// Statuses mirror `ChangeSetStatus` in the engine. The engine
  /// filter is case-sensitive so these PascalCase strings round-trip.
  statuses = ['Draft', 'Submitted', 'Approved', 'Rejected',
              'Applied', 'RolledBack', 'Cancelled'];

  statusFilter: string | null = null;
  rows: ChangeSet[] = [];
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
}
