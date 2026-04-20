import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxDateBoxModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, WhoAmI, AuditRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

interface WindowPreset {
  label: string;
  hours: number;
}

/// "What have I changed recently?" page for the current user.
/// Self-narrowed audit timeline using /api/net/whoami to resolve
/// the caller's user id, then /api/net/audit?actorUserId=… for
/// the actual rows. Service-origin callers (no X-User-Id header)
/// see an empty result + a hint banner.
@Component({
  selector: 'app-network-my-activity',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxDateBoxModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>My activity</h2>
      <small class="subtitle">
        Audit entries attributed to the current user (resolved via
        /api/net/whoami). Double-click a row to drill into the
        entity's full timeline.
      </small>
    </div>

    <div *ngIf="whoAmI && whoAmI.userId == null" class="service-banner">
      No X-User-Id header on this request — service-origin callers
      don't have a personal activity feed.
    </div>

    <div class="toolbar">
      <span class="me">User: <strong>{{ whoAmI?.userId ?? '(service)' }}</strong></span>
      <span class="me">Grants: <strong>{{ whoAmI?.grantCount ?? 0 }}</strong></span>

      <span class="sep">·</span>

      <span class="filter-label">Window:</span>
      <dx-select-box [items]="windowPresets" displayExpr="label" valueExpr="hours"
                     [(value)]="selectedWindowHours"
                     width="160"
                     (onValueChanged)="reload()" />

      <span class="filter-label">Limit:</span>
      <dx-select-box [items]="limitOptions" [(value)]="selectedLimit"
                     width="100"
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
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="sequenceId"    caption="Seq"         width="80" dataType="number"
                  sortOrder="desc" [sortIndex]="0" />
      <dxi-column dataField="createdAt"     caption="At"          width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="entityType"    caption="Entity type" width="130" />
      <dxi-column dataField="entityId"      caption="Entity id"   width="260" />
      <dxi-column dataField="action"        caption="Action"      width="120" />
      <dxi-column dataField="correlationId" caption="Correlation" width="260"
                  cellTemplate="corrTemplate" />

      <div *dxTemplate="let d of 'corrTemplate'">
        <a *ngIf="d.value" [routerLink]="['/network/audit-search']"
           [queryParams]="{ correlationId: d.value }" class="drill">
          {{ d.value | slice:0:8 }}…
        </a>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    :host { display: block; padding: 12px 16px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }

    .service-banner {
      margin: 10px 0; padding: 10px 12px; border-radius: 6px;
      background: #fff8c5; border: 1px solid #d4a72c; color: #57606a;
      font-size: 13px;
    }

    .toolbar { display: flex; gap: 10px; align-items: center; margin: 12px 0; flex-wrap: wrap; }
    .me { color: #57606a; font-size: 13px; }
    .me strong { color: #24292f; font-weight: 600; }
    .sep { color: #d0d7de; }
    .filter-label { color: #57606a; font-size: 12px; }
    .status-line { color: #666; font-size: 12px; margin-left: auto; }
    .drill { color: #3b82f6; text-decoration: none; font-family: ui-monospace, Menlo, Consolas, monospace; }
    .drill:hover { text-decoration: underline; }
  `],
})
export class NetworkMyActivityComponent implements OnInit {
  whoAmI: WhoAmI | null = null;
  rows: AuditRow[] = [];
  loading = false;
  status = '';

  windowPresets: WindowPreset[] = [
    { label: 'Last 24h',  hours: 24 },
    { label: 'Last 7d',   hours: 24 * 7 },
    { label: 'Last 30d',  hours: 24 * 30 },
    { label: 'All time',  hours: 0 },
  ];
  selectedWindowHours: number = 24 * 7;

  limitOptions = [25, 50, 100, 250, 500];
  selectedLimit = 100;

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.engine.whoAmI(environment.defaultTenantId).subscribe({
      next: (w) => {
        this.whoAmI = w;
        this.reload();
      },
      error: (err) => {
        this.status = `whoami failed: ${err?.message ?? err}`;
      },
    });
  }

  reload(): void {
    if (!this.whoAmI?.userId) {
      this.rows = [];
      return;
    }
    this.loading = true;
    this.status = 'Loading…';
    const fromAt = this.selectedWindowHours > 0
      ? new Date(Date.now() - this.selectedWindowHours * 3600_000).toISOString()
      : undefined;

    this.engine.listAudit(environment.defaultTenantId, {
      actorUserId: this.whoAmI.userId,
      fromAt,
      limit: this.selectedLimit,
    }).subscribe({
      next: (rs) => {
        this.rows = rs ?? [];
        this.loading = false;
        this.status = `${this.rows.length} row${this.rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  onRowDoubleClick(e: { data: AuditRow }): void {
    const v = e?.data;
    if (!v?.entityType || !v?.entityId) return;
    this.router.navigate(['/network/audit', v.entityType, v.entityId]);
  }
}
