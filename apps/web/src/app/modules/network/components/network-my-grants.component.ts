import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, WhoAmI, ScopeGrant,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// "What access do I have?" page for the current user.
/// Self-serve inspection of the caller's scope_grant rows via
/// /api/net/whoami/grants — bypasses the read:ScopeGrant gate so
/// any user can see their own access without needing admin rights.
/// Grouped by entity_type for scanning; double-click a row drills
/// into the scope entity (when set) so operators can verify the
/// target exists + is live.
@Component({
  selector: 'app-network-my-grants',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>My scope grants</h2>
      <small class="subtitle">
        Every net.scope_grant row attributed to the current user.
        "What can I do + where?" in one grid. Bypasses the
        read:ScopeGrant gate (reading your own access is always
        permitted).
      </small>
    </div>

    <div *ngIf="whoAmI && whoAmI.userId == null" class="service-banner">
      No X-User-Id header on this request — service-origin callers
      don't carry personal grants.
    </div>

    <div class="toolbar">
      <span class="me">User: <strong>{{ whoAmI?.userId ?? '(service)' }}</strong></span>
      <span class="me">Total: <strong>{{ grants.length }}</strong> grant{{ grants.length === 1 ? '' : 's' }}</span>
      <span class="me" *ngIf="whoAmI?.actions?.length">
        Actions: <strong>{{ whoAmI?.actions?.join(', ') }}</strong>
      </span>
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="grants" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }">
      <dxi-column dataField="entityType"    caption="Entity type" [groupIndex]="0" width="140" />
      <dxi-column dataField="action"        caption="Action"      width="110"
                  cellTemplate="actionTemplate"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="scopeType"     caption="Scope"       width="110" />
      <dxi-column dataField="scopeEntityId" caption="Scope entity" width="260" />
      <dxi-column dataField="status"        caption="Status"      width="100"
                  cellTemplate="statusTemplate" />
      <dxi-column dataField="createdAt"     caption="Granted at"  width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="notes"         caption="Notes" />

      <div *dxTemplate="let d of 'actionTemplate'">
        <span [class]="'action action-' + (d.value || '')">{{ d.value }}</span>
      </div>
      <div *dxTemplate="let d of 'statusTemplate'">
        <span [class]="'status-' + (d.value || '').toLowerCase()">{{ d.value }}</span>
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

    .toolbar { display: flex; gap: 14px; align-items: center; margin: 12px 0; flex-wrap: wrap; }
    .me { color: #57606a; font-size: 13px; }
    .me strong { color: #24292f; font-weight: 600; }
    .status-line { color: #666; font-size: 12px; }

    .action { padding: 2px 8px; border-radius: 3px; font-family: ui-monospace, Menlo, Consolas, monospace; font-size: 11px; font-weight: 600; }
    .action-read    { background: rgba(59,130,246,0.12);  color: #1d4ed8; }
    .action-write   { background: rgba(234,179,8,0.12);   color: #a16207; }
    .action-delete  { background: rgba(239,68,68,0.12);   color: #b91c1c; }
    .action-approve { background: rgba(147,51,234,0.12);  color: #6b21a8; }
    .action-apply   { background: rgba(34,197,94,0.12);   color: #15803d; }

    .status-active         { color: #15803d; font-weight: 600; }
    .status-decommissioned { color: #8b949e; }
  `],
})
export class NetworkMyGrantsComponent implements OnInit {
  whoAmI: WhoAmI | null = null;
  grants: ScopeGrant[] = [];
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
    this.engine.whoAmI(environment.defaultTenantId).subscribe({
      next: (w) => { this.whoAmI = w; this.loadGrants(); },
      error: (err) => { this.status = `whoami failed: ${err?.message ?? err}`; this.loading = false; },
    });
  }

  private loadGrants(): void {
    this.engine.listMyGrants(environment.defaultTenantId).subscribe({
      next: (rs) => {
        this.grants = rs ?? [];
        this.loading = false;
        this.status = `${this.grants.length} grant${this.grants.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Grant load failed: ${err?.message ?? err}`;
        this.grants = [];
      },
    });
  }
}
