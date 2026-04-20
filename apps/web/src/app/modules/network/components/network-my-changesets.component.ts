import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule, DxSelectBoxModule } from 'devextreme-angular';
import {
  NetworkingEngineService, WhoAmI, ChangeSet,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// "What change-sets have I authored?" page. Self-serve
/// companion to /network/my-activity and /network/my-grants.
/// Resolves the caller's userId via /api/net/whoami then calls
/// listChangeSets with requestedByUserId=that id. Covers the
/// operator's queued Draft work, pending Submitted reviews,
/// plus historical Applied/Cancelled audit trail.
@Component({
  selector: 'app-network-my-changesets',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>My change sets</h2>
      <small class="subtitle">
        Change-sets you authored (requested_by = current user).
        Resolves userId via /api/net/whoami. Service-origin
        callers see a hint banner instead of a personal list.
      </small>
    </div>

    <div *ngIf="whoAmI && whoAmI.userId == null" class="service-banner">
      No X-User-Id header on this request — service-origin callers
      don't own change-sets.
    </div>

    <div class="toolbar">
      <span class="me">User: <strong>{{ whoAmI?.userId ?? '(service)' }}</strong></span>

      <span class="sep">·</span>

      <label>Status</label>
      <dx-select-box [items]="statuses" [(value)]="statusFilter"
                     [showClearButton]="true" placeholder="(all)"
                     width="140"
                     (onValueChanged)="reload()" />

      <label>Limit</label>
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
      <dxi-column dataField="createdAt"  caption="Created"   width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" sortOrder="desc" [sortIndex]="0" />
      <dxi-column dataField="title"      caption="Title" />
      <dxi-column dataField="status"     caption="Status"    width="110"
                  cellTemplate="statusTemplate" />
      <dxi-column dataField="itemCount"  caption="Items"     width="80" dataType="number" />
      <dxi-column dataField="submittedAt" caption="Submitted" width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="appliedAt"  caption="Applied"   width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />

      <div *dxTemplate="let d of 'statusTemplate'">
        <span [class]="'pill-' + (d.value || '').toLowerCase()">{{ d.value }}</span>
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
    .toolbar label { color: #57606a; font-size: 12px; }
    .status-line { color: #666; font-size: 12px; margin-left: auto; }

    .pill-draft      { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(148,163,184,0.2); color: #cbd5e1; }
    .pill-submitted  { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(59,130,246,0.2);  color: #60a5fa; }
    .pill-approved   { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(168,85,247,0.2);  color: #a855f7; }
    .pill-applied    { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(34,197,94,0.2);   color: #22c55e; }
    .pill-rolledback { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(234,179,8,0.2);   color: #eab308; }
    .pill-rejected   { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(239,68,68,0.2);   color: #ef4444; }
    .pill-cancelled  { padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600;
                       background: rgba(107,114,128,0.2); color: #9ca3af; }
  `],
})
export class NetworkMyChangeSetsComponent implements OnInit {
  whoAmI: WhoAmI | null = null;
  rows: ChangeSet[] = [];
  loading = false;
  status = '';

  statuses = ['Draft', 'Submitted', 'Approved', 'Rejected',
              'Applied', 'RolledBack', 'Cancelled'];
  statusFilter: string | null = null;
  limitOptions = [25, 50, 100, 250, 500];
  limit = 100;

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.engine.whoAmI(environment.defaultTenantId).subscribe({
      next: (w) => { this.whoAmI = w; this.reload(); },
      error: (err) => { this.status = `whoami failed: ${err?.message ?? err}`; },
    });
  }

  reload(): void {
    if (!this.whoAmI?.userId) { this.rows = []; return; }
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listChangeSets(
      environment.defaultTenantId,
      this.statusFilter ?? undefined,
      this.limit,
      this.whoAmI.userId,
    ).subscribe({
      next: (rs) => {
        this.rows = rs ?? [];
        this.loading = false;
        this.status = `${this.rows.length} change set${this.rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  onRowDoubleClick(e: { data: ChangeSet }): void {
    const r = e?.data;
    if (!r?.id) return;
    this.router.navigate(['/network/change-sets', r.id]);
  }
}
