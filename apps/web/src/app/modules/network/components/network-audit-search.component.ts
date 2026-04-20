import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxSelectBoxModule, DxTextBoxModule,
  DxDateBoxModule, DxButtonModule, DxNumberBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, AuditRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Supported export formats for GET /api/net/audit/export — match
/// `ExportFormat` in services/networking-engine/src/audit.rs which
/// uses `#[serde(rename_all = "lowercase")]`.
type ExportFormat = 'csv' | 'ndjson';

/// Free-form audit log search — companion to the per-entity timeline.
/// Where `/network/audit/:entityType/:entityId` answers "what happened
/// to this one thing", this page answers "what happened across the
/// tenant matching these filters". The target of the audit-stats page
/// double-click drill (entityType query param auto-populates).
///
/// Mirrors the WPF AuditViewerPanel's filter bar: entity type / entity
/// id / action / actor / correlation id / date window / limit, with
/// 1h / 24h / 7d / 30d preset chips for the window. No cursor
/// pagination yet — a follow-up once the limit-500 hit shows up as a
/// pain point in the field.
@Component({
  selector: 'app-network-audit-search',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxSelectBoxModule, DxTextBoxModule,
            DxDateBoxModule, DxButtonModule, DxNumberBoxModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/audit-stats" class="back-link">← Audit stats</a>
      <h2>Audit search</h2>
      <small class="subtitle">Free-form filter across every net.* mutation. Double-click a row to jump to the entity's full timeline.</small>
    </div>

    <div class="filter-bar">
      <label>Entity type</label>
      <dx-select-box class="md" [items]="knownEntityTypes" [(value)]="entityType"
                     [showClearButton]="true" placeholder="(any)" [acceptCustomValue]="true" />

      <label>Entity id</label>
      <dx-text-box class="lg" [(value)]="entityId" placeholder="(any)" />

      <label>Action</label>
      <dx-select-box class="md" [items]="knownActions" [(value)]="action"
                     [showClearButton]="true" placeholder="(any)" [acceptCustomValue]="true" />

      <label>Actor</label>
      <dx-number-box class="sm" [(value)]="actorUserId" [showClearButton]="true"
                     placeholder="(any)" [showSpinButtons]="false" />

      <label>Correlation</label>
      <dx-text-box class="lg" [(value)]="correlationId" placeholder="(any)" />

      <label>Limit</label>
      <dx-number-box class="sm" [(value)]="limit" [min]="1" [max]="500"
                     [showSpinButtons]="true" />
    </div>

    <div class="filter-bar">
      <label>From</label>
      <dx-date-box [(value)]="fromAt" type="datetime" [showClearButton]="true"
                   displayFormat="yyyy-MM-dd HH:mm" placeholder="(any)" />
      <label>To</label>
      <dx-date-box [(value)]="toAt" type="datetime" [showClearButton]="true"
                   displayFormat="yyyy-MM-dd HH:mm" placeholder="(any)" />

      <dx-button text="1h"  stylingMode="outlined" (onClick)="windowHours(1)" />
      <dx-button text="24h" stylingMode="outlined" (onClick)="windowHours(24)" />
      <dx-button text="7d"  stylingMode="outlined" (onClick)="windowDays(7)" />
      <dx-button text="30d" stylingMode="outlined" (onClick)="windowDays(30)" />

      <dx-button text="Search" type="default" (onClick)="reload()" />
      <dx-button text="Clear" (onClick)="clear()" />

      <span class="spacer"></span>

      <dx-button text="Export CSV" icon="exportxlsx" stylingMode="outlined"
                 hint="Download every audit row matching the current filter (up to 50k)"
                 (onClick)="exportRows('csv')" />
      <dx-button text="Export NDJSON" stylingMode="outlined"
                 hint="Download rows as newline-delimited JSON for streaming pipelines"
                 (onClick)="exportRows('ndjson')" />
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="sequenceId"    caption="Seq"         width="80"  dataType="number"
                  [sortIndex]="0" sortOrder="desc" />
      <dxi-column dataField="createdAt"     caption="At"          width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="entityType"    caption="Entity type" width="130" />
      <dxi-column dataField="entityId"      caption="Entity id"   width="260" />
      <dxi-column dataField="action"        caption="Action"      width="120" />
      <dxi-column dataField="actorDisplay"  caption="Actor"       width="140" />
      <dxi-column dataField="correlationId" caption="Correlation" width="240" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .filter-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 8px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .sm { width: 100px; }
    .filter-bar .md { width: 180px; }
    .filter-bar .lg { width: 260px; }
    .filter-bar .spacer { flex: 1; min-width: 12px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
  `]
})
export class NetworkAuditSearchComponent implements OnInit {
  knownEntityTypes = [
    'Region', 'Site', 'Building', 'Floor', 'Room', 'Rack',
    'Device', 'Server', 'Link', 'Vlan', 'Subnet',
    'DhcpRelayTarget', 'ScopeGrant', 'SavedView', 'ChangeSet',
  ];
  /// Seed list of "known" action strings. Replaced at ngOnInit
  /// from /api/net/audit/actions with the live tenant set so
  /// rarely-used actions (e.g. ItemRemoved, RolledBack) surface
  /// in the dropdown when they've actually happened.
  knownActions: string[] = [
    'Created', 'Updated', 'Deleted', 'Applied', 'Rendered',
    'Imported', 'Validated', 'Locked', 'Unlocked',
  ];

  entityType: string | null = null;
  entityId = '';
  action: string | null = null;
  actorUserId: number = 0;
  correlationId = '';
  fromAt: Date | null = null;
  toAt: Date | null = null;
  limit = 100;

  rows: AuditRow[] = [];
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    // Pick up any query params set by the drill from audit-stats.
    const qp = this.route.snapshot.queryParamMap;
    this.entityType = qp.get('entityType') || null;
    this.entityId   = qp.get('entityId')   || '';
    this.action     = qp.get('action')     || null;
    const actorRaw  = qp.get('actorUserId');
    if (actorRaw) {
      const n = Number(actorRaw);
      this.actorUserId = Number.isFinite(n) && n > 0 ? n : 0;
    }
    this.correlationId = qp.get('correlationId') || '';

    // Pull the live distinct-action set so the dropdown reflects
    // what this tenant has actually emitted (not just the seeded
    // default). Runs in parallel with reload(); order doesn't
    // matter because the dropdown binding is observable in Angular.
    this.engine.listAuditActions(environment.defaultTenantId, undefined, 200)
      .subscribe({
        next: (rows) => {
          if (rows?.length) {
            this.knownActions = rows.map(r => r.action);
          }
        },
        error: () => { /* silent — keep the seeded defaults */ },
      });

    this.reload();
  }

  reload(): void {
    this.status = 'Loading…';
    this.engine.listAudit(environment.defaultTenantId, {
      entityType:    this.entityType ?? undefined,
      entityId:      this.entityId.trim() || undefined,
      action:        this.action ?? undefined,
      actorUserId:   this.actorUserId > 0 ? this.actorUserId : undefined,
      correlationId: this.correlationId.trim() || undefined,
      fromAt:        this.fromAt ? this.fromAt.toISOString() : undefined,
      toAt:          this.toAt   ? this.toAt.toISOString()   : undefined,
      limit:         this.limit,
    }).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.status = `${rows.length} row${rows.length === 1 ? '' : 's'}` +
          (rows.length >= this.limit ? ` (hit limit ${this.limit} — narrow the filter)` : '');
      },
      error: (err) => {
        this.status = `Search failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  windowHours(h: number): void {
    const now = new Date();
    this.fromAt = new Date(now.getTime() - h * 3_600_000);
    this.toAt = now;
    this.reload();
  }

  windowDays(d: number): void {
    this.windowHours(d * 24);
  }

  clear(): void {
    this.entityType = null;
    this.entityId = '';
    this.action = null;
    this.actorUserId = 0;
    this.correlationId = '';
    this.fromAt = null;
    this.toAt = null;
    this.limit = 100;
    this.reload();
  }

  /// Double-click → switch to the per-entity timeline for that row.
  /// The timeline view has no filter bar, no 500-row cap, and is the
  /// right drill once you've found the row of interest in the search.
  onRowDoubleClick(e: { data: AuditRow }): void {
    const r = e?.data;
    if (!r?.entityType || !r?.entityId) return;
    this.router.navigate(['/network/audit', r.entityType, r.entityId]);
  }

  /// Trigger a download by opening the engine's `/api/net/audit/export`
  /// URL with the current filter as query params. The engine sets
  /// Content-Disposition: attachment so the browser downloads rather
  /// than navigating away. Server caps at 50k rows by default
  /// (`default_export_limit`) — bigger pulls should chunk by window.
  ///
  /// Opens in a new window via `window.open(..., '_blank')` so the
  /// operator's audit-search page state is preserved if the download
  /// fails and the engine responds with HTML error page.
  exportRows(format: ExportFormat): void {
    const params = new URLSearchParams();
    params.set('organizationId', environment.defaultTenantId);
    params.set('format', format);
    if (this.entityType)                 params.set('entityType',   this.entityType);
    if (this.entityId.trim())            params.set('entityId',     this.entityId.trim());
    if (this.action)                     params.set('action',       this.action);
    if (this.actorUserId !== null)       params.set('actorUserId',  String(this.actorUserId));
    if (this.correlationId.trim())       params.set('correlationId', this.correlationId.trim());
    if (this.fromAt)                     params.set('fromAt',       this.fromAt.toISOString());
    if (this.toAt)                       params.set('toAt',         this.toAt.toISOString());

    const url = `${environment.networkingEngineUrl}/api/net/audit/export?${params.toString()}`;
    if (typeof window !== 'undefined') {
      window.open(url, '_blank');
    }
  }
}
