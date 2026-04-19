import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxTextBoxModule, DxButtonModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ScopeGrant,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Read-only web view of net.scope_grant. Complements the WPF
/// ScopeGrantsAdminPanel (e198af3dc); this slice surfaces the list
/// + filter + drill-down without the create / delete / clone
/// dialogs — those land alongside a form component in a follow-up.
@Component({
  selector: 'app-network-scope-grants',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxTextBoxModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <h2>Scope grants</h2>
      <small class="subtitle">RBAC tuples that gate every scope-aware engine endpoint. Read-only today; write via the WPF admin panel.</small>
    </div>

    <div class="filter-bar">
      <label>User id</label>
      <dx-text-box class="sm" placeholder="(any)" [(value)]="userIdFilter" />
      <dx-button text="Me" hint="Filter to grants for your own user id"
                 stylingMode="outlined" (onClick)="setForMe()" />

      <label>Action</label>
      <dx-select-box class="md" [items]="knownActions" [(value)]="actionFilter"
                     [showClearButton]="true" placeholder="(any)" acceptCustomValue="true" />

      <label>Entity type</label>
      <dx-select-box class="md" [items]="knownEntityTypes" [(value)]="entityTypeFilter"
                     [showClearButton]="true" placeholder="(any)" acceptCustomValue="true" />

      <dx-button text="Filter" type="default" (onClick)="reload()" />
      <dx-button text="Clear" (onClick)="clear()" />
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-data-grid [dataSource]="grants" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="userId"        caption="User"         width="80" />
      <dxi-column dataField="action"        caption="Action"       width="100" />
      <dxi-column dataField="entityType"    caption="Entity"       width="130" />
      <dxi-column dataField="scopeType"     caption="Scope"        width="100" />
      <dxi-column dataField="scopeEntityId" caption="Scope id"     width="260" />
      <dxi-column dataField="status"        caption="Status"       width="80" />
      <dxi-column dataField="notes"         caption="Notes" />
      <dxi-column dataField="id"            caption="Grant id"     width="260" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .filter-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 10px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .sm { width: 100px; }
    .filter-bar .md { width: 180px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
  `]
})
export class NetworkScopeGrantsComponent implements OnInit {
  /// Mirror the WPF `KnownActions` / `KnownEntityTypes`. `acceptCustomValue`
  /// on the DxSelectBox lets operators type unknown values since the
  /// engine is free-text-tolerant for forward compat.
  knownActions = ['read', 'write', 'delete', 'apply', 'render'];
  knownEntityTypes = [
    'Region', 'Site', 'Building', 'Device', 'Server', 'Link',
    'Vlan', 'Subnet', 'DhcpRelayTarget',
  ];

  userIdFilter = '';
  actionFilter: string | null = null;
  entityTypeFilter: string | null = null;
  grants: ScopeGrant[] = [];
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    const uid = this.userIdFilter.trim() ? Number(this.userIdFilter.trim()) : undefined;
    if (uid !== undefined && (!Number.isFinite(uid) || uid <= 0)) {
      this.status = 'User id must be a positive integer.';
      return;
    }
    this.status = 'Loading…';
    this.engine
      .listScopeGrants(
        environment.defaultTenantId,
        uid,
        this.actionFilter ?? undefined,
        this.entityTypeFilter ?? undefined,
      )
      .subscribe({
        next: (rows) => {
          this.grants = rows;
          this.status = `${rows.length} grant${rows.length === 1 ? '' : 's'}`;
        },
        error: (err) => {
          this.status = `Load failed: ${err?.message ?? err}`;
          this.grants = [];
        },
      });
  }

  clear(): void {
    this.userIdFilter = '';
    this.actionFilter = null;
    this.entityTypeFilter = null;
    this.reload();
  }

  /// Set the user-id filter to the current actor's id. Mirrors the
  /// WPF "Me" button (ba5a953b7). Reads from localStorage since the
  /// web client's AuthService stashes the user-id there after login;
  /// null / non-numeric → status-bar nudge.
  setForMe(): void {
    const raw = typeof window !== 'undefined' ? window.localStorage.getItem('userId') : null;
    const n = raw ? Number(raw) : NaN;
    if (!Number.isFinite(n) || n <= 0) {
      this.status = 'No current user id in session — log in or switch accounts first.';
      return;
    }
    this.userIdFilter = n.toString();
    this.reload();
  }

  /// Double-click drill to the audit timeline for this grant — same
  /// pattern the WPF row context menu uses. ScopeGrant writes emit
  /// AuditEvent entity_type="ScopeGrant", so the timeline shows the
  /// create + any future edits + (soft) delete.
  onRowDoubleClick(e: { data: ScopeGrant }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/audit', 'ScopeGrant', row.id]);
  }
}
