import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ChangeSet, ChangeSetItem,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Detail view of one change-set — header row + item list. Landed
/// from the change-sets list page (double-click → audit-search
/// drill), OR directly via /network/change-sets/:id for bookmarking.
/// Read-only in Phase 10b; submit / approve / apply / cancel /
/// rollback stay WPF-only.
///
/// Item list shows the before/after JSON snapshots inline so an
/// operator can scan a Set's full payload without drilling into
/// every entity. Pretty-prints via JSON.stringify with 2-space
/// indent; truncation is manual (scroll the code block) rather than
/// server-side.
@Component({
  selector: 'app-network-change-set-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/change-sets" class="back-link">← Change sets</a>
      <h2>Change set detail</h2>
      <small *ngIf="set" class="subtitle">
        <span [class]="'badge badge-' + statusBadgeClass(set.status)">{{ set.status }}</span>
        · {{ set.title }}
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <div *ngIf="set" class="meta-grid">
      <div class="meta-row">
        <label>Requested by</label>
        <span>{{ set.requestedByDisplay ?? '—' }}<span *ngIf="set.requestedBy !== null" class="sub">(user id {{ set.requestedBy }})</span></span>
      </div>
      <div class="meta-row">
        <label>Approvals needed</label>
        <span>{{ set.requiredApprovals ?? '—' }}</span>
      </div>
      <div class="meta-row">
        <label>Items</label>
        <span>{{ set.itemCount }}</span>
      </div>
      <div class="meta-row">
        <label>Correlation</label>
        <a href="javascript:void(0)" class="correlation-link"
           (click)="drillCorrelation(set.correlationId)">{{ set.correlationId }}</a>
      </div>
      <div class="meta-row">
        <label>Submitted</label>
        <span>{{ formatTs(set.submittedAt) }}</span>
      </div>
      <div class="meta-row">
        <label>Approved</label>
        <span>{{ formatTs(set.approvedAt) }}</span>
      </div>
      <div class="meta-row">
        <label>Applied</label>
        <span>{{ formatTs(set.appliedAt) }}</span>
      </div>
      <div class="meta-row">
        <label>Rolled back</label>
        <span>{{ formatTs(set.rolledBackAt) }}</span>
      </div>
      <div class="meta-row">
        <label>Cancelled</label>
        <span>{{ formatTs(set.cancelledAt) }}</span>
      </div>
      <div class="meta-row full" *ngIf="set.description">
        <label>Description</label>
        <span>{{ set.description }}</span>
      </div>
    </div>

    <h3 class="section-title">Items</h3>
    <dx-data-grid [dataSource]="items" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [masterDetail]="{ enabled: true, template: 'jsonDetail' }">
      <dxi-column dataField="itemOrder"      caption="#"               width="60"  dataType="number" sortOrder="asc" />
      <dxi-column dataField="action"         caption="Action"          width="100" />
      <dxi-column dataField="entityType"     caption="Entity type"     width="150" />
      <dxi-column dataField="entityId"       caption="Entity id"       width="260" />
      <dxi-column dataField="expectedVersion" caption="Expected v"     width="100" dataType="number" />
      <dxi-column dataField="appliedAt"      caption="Applied"         width="160" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="applyError"     caption="Apply error"     cellTemplate="errorTemplate" />
      <dxi-column dataField="notes"          caption="Notes" />

      <div *dxTemplate="let d of 'errorTemplate'">
        <span *ngIf="d.value" class="apply-error">{{ d.value }}</span>
      </div>

      <div *dxTemplate="let d of 'jsonDetail'">
        <div class="json-panels">
          <div class="json-panel">
            <div class="json-label">Before</div>
            <pre class="json-blob">{{ prettyJson(d.data.beforeJson) }}</pre>
          </div>
          <div class="json-panel">
            <div class="json-label">After</div>
            <pre class="json-blob">{{ prettyJson(d.data.afterJson) }}</pre>
          </div>
        </div>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; display: inline-flex; align-items: center; gap: 8px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-draft      { background: rgba(148,163,184,0.2); color: #cbd5e1; }
    .badge-submitted  { background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .badge-approved   { background: rgba(168,85,247,0.2);  color: #a855f7; }
    .badge-rejected   { background: rgba(239,68,68,0.2);   color: #ef4444; }
    .badge-applied    { background: rgba(34,197,94,0.2);   color: #22c55e; }
    .badge-rolledback { background: rgba(234,179,8,0.2);   color: #eab308; }
    .badge-cancelled  { background: rgba(107,114,128,0.2); color: #9ca3af; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; margin-bottom: 20px; padding: 12px; background: #1e293b; border-radius: 6px; font-size: 13px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row .sub { color: #64748b; font-size: 11px; margin-left: 6px; }
    .section-title { color: #9ca3af; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; margin: 20px 0 8px; font-weight: 600; }
    .correlation-link { color: #60a5fa; font-family: ui-monospace, monospace; font-size: 11px; text-decoration: none; word-break: break-all; }
    .correlation-link:hover { text-decoration: underline; }
    .apply-error { color: #ef4444; font-family: ui-monospace, monospace; font-size: 11px; }
    .json-panels { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; padding: 8px; }
    .json-panel { background: #0f172a; border-radius: 4px; padding: 8px; }
    .json-label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; margin-bottom: 4px; }
    .json-blob { font-family: ui-monospace, monospace; font-size: 11px; color: #cbd5e1; margin: 0; white-space: pre; overflow-x: auto; max-height: 300px; }
    @media (max-width: 1100px) {
      .meta-grid { grid-template-columns: 1fr 1fr; }
      .json-panels { grid-template-columns: 1fr; }
    }
  `]
})
export class NetworkChangeSetDetailComponent implements OnInit {
  set: ChangeSet | null = null;
  items: ChangeSetItem[] = [];
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    if (!id) {
      this.status = 'Missing route param — expected /network/change-sets/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.getChangeSet(id, environment.defaultTenantId).subscribe({
      next: (envelope) => {
        this.set = envelope.set;
        this.items = envelope.items;
        this.status = '';
      },
      error: (err) => {
        this.status = err?.status === 404
          ? 'Change set not found.'
          : `Load failed: ${err?.message ?? err}`;
        this.set = null;
        this.items = [];
      },
    });
  }

  statusBadgeClass(status: string): string {
    return (status || 'draft').toLowerCase();
  }

  formatTs(iso: string | null): string {
    if (!iso) return '—';
    try {
      const d = new Date(iso);
      return d.toLocaleString();
    } catch {
      return iso;
    }
  }

  /// JSON.stringify with fallback for non-object values. '—' for
  /// null so the before/after panes render empty rather than "null".
  prettyJson(value: unknown): string {
    if (value === null || value === undefined) return '—';
    try {
      return JSON.stringify(value, null, 2);
    } catch {
      return String(value);
    }
  }

  drillCorrelation(correlationId: string): void {
    if (!correlationId) return;
    this.router.navigate(['/network/audit-search'], {
      queryParams: { correlationId },
    });
  }
}
