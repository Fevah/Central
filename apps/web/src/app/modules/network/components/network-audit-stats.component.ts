import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxDateBoxModule, DxButtonModule, DxChartModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, EntityTypeStats, AuditTrendPoint, TopActor,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Audit activity summary — one row per entity type with total count,
/// distinct actor count, last-seen-at. Thin web surface over the
/// engine's GET /api/net/audit/stats. Mirrors the per-entity-type
/// breakdown the WPF AuditDashboardPanel shows, without the chart
/// widgets (plan for a follow-up once the stats shape stabilises).
@Component({
  selector: 'app-network-audit-stats',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxDateBoxModule, DxButtonModule, DxChartModule],
  template: `
    <div class="page-header">
      <h2>Audit activity</h2>
      <small class="subtitle">One row per entity type within the selected window. Double-click a row to jump to the audit log filtered to that type.</small>
    </div>

    <div class="filter-bar">
      <label>From</label>
      <dx-date-box [(value)]="fromAt" type="datetime" [showClearButton]="true"
                   displayFormat="yyyy-MM-dd HH:mm" placeholder="(any)" />
      <label>To</label>
      <dx-date-box [(value)]="toAt" type="datetime" [showClearButton]="true"
                   displayFormat="yyyy-MM-dd HH:mm" placeholder="(any)" />

      <dx-button text="Last 24h"  stylingMode="outlined" (onClick)="preset(1)" />
      <dx-button text="Last 7d"   stylingMode="outlined" (onClick)="preset(7)" />
      <dx-button text="Last 30d"  stylingMode="outlined" (onClick)="preset(30)" />

      <dx-button text="Refresh" type="default" (onClick)="reload()" />
      <dx-button text="Clear" (onClick)="clear()" />
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <!-- Time-bucketed activity chart. Bucket granularity follows the
         window automatically: <= 48h → hour, <= 90d → day, > 90d →
         week. -->
    <dx-chart *ngIf="trend.length"
              [dataSource]="trend"
              title="Audit events over time"
              class="trend-chart">
      <dxo-series
        argumentField="bucketAt"
        valueField="count"
        type="line"
        name="Events">
      </dxo-series>
      <dxo-argument-axis argumentType="datetime"></dxo-argument-axis>
      <dxo-value-axis></dxo-value-axis>
      <dxo-common-series-settings [point]="{ visible: false }"></dxo-common-series-settings>
    </dx-chart>

    <div class="two-col">
      <div class="col">
        <h3 class="section-title">By entity type</h3>
        <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                       [columnAutoWidth]="true"
                       [searchPanel]="{ visible: true }"
                       [filterRow]="{ visible: true }"
                       [headerFilter]="{ visible: true }"
                       (onRowDblClick)="onRowDoubleClick($event)">
          <dxi-column dataField="entityType"     caption="Entity type"     width="180" />
          <dxi-column dataField="totalCount"     caption="Events"          width="110" dataType="number" sortOrder="desc" />
          <dxi-column dataField="distinctActors" caption="Distinct actors" width="140" dataType="number" />
          <dxi-column dataField="lastSeenAt"     caption="Last seen"       dataType="datetime" />
        </dx-data-grid>
      </div>

      <div class="col">
        <h3 class="section-title">Top actors</h3>
        <dx-data-grid [dataSource]="topActors" [showBorders]="true" [hoverStateEnabled]="true"
                       [columnAutoWidth]="true"
                       [searchPanel]="{ visible: true }"
                       [filterRow]="{ visible: true }"
                       (onRowDblClick)="onTopActorDoubleClick($event)">
          <dxi-column dataField="actorDisplayOrService" caption="Actor"             width="180" />
          <dxi-column dataField="actorUserId"           caption="User id"           width="80"  dataType="number" />
          <dxi-column dataField="totalCount"            caption="Events"            width="100" dataType="number" sortOrder="desc" />
          <dxi-column dataField="distinctEntityTypes"   caption="Entity types"      width="120" dataType="number" />
          <dxi-column dataField="lastSeenAt"            caption="Last seen"         dataType="datetime" />
        </dx-data-grid>
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .filter-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 10px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .trend-chart { height: 240px; margin-bottom: 16px; }
    .two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; }
    .section-title { color: #9ca3af; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; margin: 0 0 6px; font-weight: 600; }
    @media (max-width: 1100px) { .two-col { grid-template-columns: 1fr; } }
  `]
})
export class NetworkAuditStatsComponent implements OnInit {
  fromAt: Date | null = null;
  toAt: Date | null = null;
  rows: EntityTypeStats[] = [];
  trend: AuditTrendPoint[] = [];
  /// Decorated with `actorDisplayOrService` so the grid shows
  /// "(service)" for rows where actor_display is NULL instead of a
  /// blank cell.
  topActors: Array<TopActor & { actorDisplayOrService: string }> = [];
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.preset(7);
  }

  reload(): void {
    this.status = 'Loading…';
    const from = this.fromAt ? this.fromAt.toISOString() : undefined;
    const to   = this.toAt   ? this.toAt.toISOString()   : undefined;
    this.engine
      .auditStatsByEntityType(environment.defaultTenantId, from, to)
      .subscribe({
        next: (rows) => {
          this.rows = rows;
          const total = rows.reduce((a, r) => a + (r.totalCount || 0), 0);
          this.status = `${rows.length} entity type${rows.length === 1 ? '' : 's'} · ${total.toLocaleString()} event${total === 1 ? '' : 's'}`;
        },
        error: (err) => {
          this.status = `Load failed: ${err?.message ?? err}`;
          this.rows = [];
        },
      });

    // Trend loads in parallel — independent chart, independent error
    // path. Bucket granularity picked from the window length so the
    // chart is always readable (~20-200 points). Fallback when both
    // ends are null → day (all-time view keeps the buckets sane).
    const bucket = this.pickBucket();
    this.engine.auditTrend(environment.defaultTenantId, {
      fromAt: from, toAt: to, bucketBy: bucket,
    }).subscribe({
      next: (pts) => {
        // DxChart parses datetime strings but prefers real Date
        // objects for consistent tick behaviour across timezones.
        this.trend = pts.map(p => ({
          bucketAt: p.bucketAt,
          count:    p.count,
        }));
      },
      error: () => { this.trend = []; },
    });

    // Top actors panel — same window, leaderboard-length limit.
    this.engine.auditTopActors(environment.defaultTenantId, {
      fromAt: from, toAt: to, limit: 20,
    }).subscribe({
      next: (actors) => {
        this.topActors = actors.map(a => ({
          ...a,
          actorDisplayOrService: a.actorDisplay ?? '(service)',
        }));
      },
      error: () => { this.topActors = []; },
    });
  }

  /// Double-click a top-actor row → audit search pre-filtered to
  /// this actor. Null actorUserId (service rows) has no drill target.
  onTopActorDoubleClick(e: { data: TopActor }): void {
    const a = e?.data;
    if (a?.actorUserId === null || a?.actorUserId === undefined) return;
    this.router.navigate(['/network/audit-search'], {
      queryParams: { actorUserId: a.actorUserId },
    });
  }

  private pickBucket(): 'hour' | 'day' | 'week' {
    if (!this.fromAt || !this.toAt) return 'day';
    const spanMs = this.toAt.getTime() - this.fromAt.getTime();
    const spanDays = spanMs / 86_400_000;
    if (spanDays <= 2)   return 'hour';
    if (spanDays <= 90)  return 'day';
    return 'week';
  }

  /// Set fromAt to N days ago, toAt to now. Matches the WPF audit
  /// panel's preset buttons — keeps the web + desktop muscle memory
  /// aligned.
  preset(days: number): void {
    const now = new Date();
    const from = new Date(now.getTime() - days * 86_400_000);
    this.fromAt = from;
    this.toAt = now;
    this.reload();
  }

  clear(): void {
    this.fromAt = null;
    this.toAt = null;
    this.reload();
  }

  /// Double-click a row → navigate to the audit log search scoped to
  /// that entity type. Uses the existing `q:entityType:X` payload the
  /// audit-timeline component understands (same vocabulary as the
  /// WPF cross-panel drill messages).
  onRowDoubleClick(e: { data: EntityTypeStats }): void {
    const t = e?.data?.entityType;
    if (!t) return;
    this.router.navigate(['/network/audit-search'], { queryParams: { entityType: t } });
  }
}
