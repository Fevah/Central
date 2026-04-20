import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule, DxSelectBoxModule } from 'devextreme-angular';
import {
  NetworkingEngineService, RecentCorrelation,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// "What bulk operations landed lately?" page. Every change-set
/// apply, bulk edit, allocation retire, etc. stamps a correlation
/// id onto its audit entries; this page groups by that id so
/// operators can scan at the operation level rather than the
/// per-entry level.
///
/// Double-click a row:
/// - setId present → drill to /network/change-sets/:id detail page
/// - setId null    → drill to /network/audit-search with
///                   correlationId=... pre-filled (the default
///                   correlation view is audit-search).
@Component({
  selector: 'app-network-correlations',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>Recent correlations</h2>
      <small class="subtitle">
        Distinct correlation ids across the tenant — one row per
        bulk operation. Change-set applies populate setTitle +
        status; ad-hoc operations (allocation retires, bulk edits
        without a wrapper) show the correlation without a set.
      </small>
    </div>

    <div class="toolbar">
      <label>Window:</label>
      <dx-select-box [items]="windowPresets" displayExpr="label" valueExpr="hours"
                     [(value)]="windowHours" width="160"
                     (onValueChanged)="reload()" />

      <label>Limit:</label>
      <dx-select-box [items]="limitOptions" [(value)]="limit"
                     width="100" (onValueChanged)="reload()" />

      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />

      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="lastSeenAt"   caption="Last seen"   width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" sortOrder="desc" [sortIndex]="0" />
      <dxi-column dataField="setTitle"     caption="Change set"  width="260"
                  cellTemplate="setTemplate" />
      <dxi-column dataField="setStatus"    caption="Status"      width="120"
                  cellTemplate="statusTemplate" />
      <dxi-column dataField="entryCount"   caption="Entries"     width="100" dataType="number" />
      <dxi-column dataField="distinctEntityTypes" caption="Entity types"
                  width="130" dataType="number" />
      <dxi-column dataField="firstSeenAt"  caption="First seen"  width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="correlationId" caption="Correlation"
                  cellTemplate="corrTemplate" />

      <div *dxTemplate="let d of 'setTemplate'">
        <ng-container *ngIf="d.data.setTitle; else adhoc">
          <span class="set-title">{{ d.data.setTitle }}</span>
        </ng-container>
        <ng-template #adhoc>
          <span class="adhoc">(ad-hoc)</span>
        </ng-template>
      </div>
      <div *dxTemplate="let d of 'statusTemplate'">
        <span *ngIf="d.value" [class]="'status-' + d.value.toLowerCase()">{{ d.value }}</span>
        <span *ngIf="!d.value" class="muted">—</span>
      </div>
      <div *dxTemplate="let d of 'corrTemplate'">
        <code class="correlation">{{ d.value | slice:0:8 }}…</code>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    :host { display: block; padding: 12px 16px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 10px; align-items: center; margin: 12px 0; }
    .toolbar label { color: #57606a; font-size: 13px; }
    .status-line { color: #666; font-size: 12px; margin-left: auto; }

    .set-title { font-weight: 500; }
    .adhoc     { color: #8b949e; font-style: italic; font-size: 12px; }
    .muted     { color: #8b949e; }
    .correlation {
      font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 11px;
      background: rgba(148,163,184,0.1); padding: 1px 6px; border-radius: 3px;
    }
    .status-draft     { color: #0969da; }
    .status-submitted { color: #bf8700; }
    .status-approved  { color: #1a7f37; }
    .status-applied   { color: #1a7f37; font-weight: 600; }
    .status-rolledback, .status-cancelled { color: #8b949e; }
    .status-rejected  { color: #cf222e; }
  `],
})
export class NetworkCorrelationsComponent implements OnInit {
  rows: RecentCorrelation[] = [];
  loading = false;
  status = '';

  limitOptions = [25, 50, 100, 250, 500];
  limit = 50;

  windowPresets = [
    { label: 'Last 24h',  hours: 24 },
    { label: 'Last 7d',   hours: 24 * 7 },
    { label: 'Last 30d',  hours: 24 * 30 },
    { label: 'All time',  hours: 0 },
  ];
  windowHours = 24 * 7;

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    const fromAt = this.windowHours > 0
      ? new Date(Date.now() - this.windowHours * 3_600_000).toISOString()
      : undefined;
    this.engine.listRecentCorrelations(environment.defaultTenantId, this.limit, fromAt)
      .subscribe({
        next: (rs) => {
          this.rows = rs ?? [];
          this.loading = false;
          this.status = `${this.rows.length} correlation${this.rows.length === 1 ? '' : 's'}`;
        },
        error: (err) => {
          this.loading = false;
          this.status = `Load failed: ${err?.message ?? err}`;
          this.rows = [];
        },
      });
  }

  onRowDoubleClick(e: { data: RecentCorrelation }): void {
    const r = e?.data;
    if (!r) return;
    if (r.setId) {
      this.router.navigate(['/network/change-sets', r.setId]);
    } else {
      this.router.navigate(['/network/audit-search'], {
        queryParams: { correlationId: r.correlationId },
      });
    }
  }
}
