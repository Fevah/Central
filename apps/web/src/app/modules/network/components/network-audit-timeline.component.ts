import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, AuditRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF Audit panel's "Full timeline" button —
/// hits `/api/net/audit/entity/{type}/{id}` which returns the
/// complete history ordered by timestamp (no 500-row cap). Route
/// params supply entity type + uuid; same shape the WPF panel uses
/// for its `selectEntity:{Type}:{Guid}` drill-down payload.
@Component({
  selector: 'app-network-audit-timeline',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <a routerLink="/network" class="back-link">← Network</a>
      <h2>Audit timeline</h2>
      <small class="subtitle">
        {{ entityType }} · <code>{{ entityId }}</code>
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }" [searchPanel]="{ visible: true }"
                   [allowColumnResizing]="true">
      <dxi-column dataField="sequenceId"    caption="Seq"          width="70" />
      <dxi-column dataField="createdAt"     caption="At"           width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" [sortIndex]="0" sortOrder="desc" />
      <dxi-column dataField="action"        caption="Action"       width="120" />
      <dxi-column dataField="actorDisplay"  caption="Actor"        width="140" />
      <dxi-column dataField="correlationId" caption="Correlation"  width="240" />
      <dxi-column dataField="details"       caption="Details"      cellTemplate="detailsTemplate" />

      <div *dxTemplate="let d of 'detailsTemplate'">
        <code class="details-blob">{{ summariseDetails(d.value) }}</code>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .subtitle code { background: #1e293b; padding: 1px 6px; border-radius: 3px; font-size: 11px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .details-blob { font-size: 11px; color: #aaa; }
  `]
})
export class NetworkAuditTimelineComponent implements OnInit {
  entityType = '';
  entityId = '';
  rows: AuditRow[] = [];
  status = '';

  constructor(
    private route: ActivatedRoute,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.entityType = this.route.snapshot.paramMap.get('entityType') ?? '';
    this.entityId   = this.route.snapshot.paramMap.get('entityId')   ?? '';

    if (!this.entityType || !this.entityId) {
      this.status = 'Missing route params — expected /network/audit/:entityType/:entityId.';
      return;
    }

    this.status = 'Loading…';
    this.engine
      .getEntityTimeline(environment.defaultTenantId, this.entityType, this.entityId)
      .subscribe({
        next: (rows) => {
          this.rows = rows;
          this.status = rows.length === 0
            ? `No audit rows for ${this.entityType} ${this.entityId}.`
            : `${rows.length} row${rows.length === 1 ? '' : 's'}`;
        },
        error: (err) => {
          this.status = `Fetch failed: ${err?.message ?? err}`;
          this.rows = [];
        },
      });
  }

  /// Render the jsonb details blob as a truncated one-liner — same
  /// shape the WPF panel uses. Real jsonb inspection would need a
  /// tree view; this is the quick-scan render.
  summariseDetails(details: unknown): string {
    if (!details || typeof details !== 'object') return '';
    try {
      const s = JSON.stringify(details);
      return s.length > 120 ? s.slice(0, 120) + '…' : s;
    } catch {
      return '';
    }
  }
}
