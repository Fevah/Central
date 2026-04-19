import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxTextBoxModule, DxButtonModule, DxSelectBoxModule,
  DxPopupModule, DxNumberBoxModule, DxTextAreaModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ScopeGrant, PermissionDecision,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web view of net.scope_grant — list / filter / create / delete.
/// Parallel to the WPF ScopeGrantsAdminPanel (e198af3dc).
///
/// `write:ScopeGrant` is the bootstrap permission gating creation;
/// the API returns 403 for unauthorised users so the Save button
/// surfaces a friendly error rather than silently failing.
@Component({
  selector: 'app-network-scope-grants',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxTextBoxModule, DxButtonModule, DxSelectBoxModule,
            DxPopupModule, DxNumberBoxModule, DxTextAreaModule],
  template: `
    <div class="page-header">
      <h2>Scope grants</h2>
      <small class="subtitle">RBAC tuples that gate every scope-aware engine endpoint. Create requires write:ScopeGrant.</small>
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

      <span class="spacer"></span>

      <dx-button text="Check permission" icon="find" stylingMode="outlined"
                 hint="Dry-run the permission resolver — 'would this user be allowed to do X?'"
                 (onClick)="openCheckDialog()" />

      <dx-button text="New grant" icon="add" type="default"
                 stylingMode="contained" (onClick)="openCreateDialog()" />
    </div>

    <!-- Create dialog. DxPopup mounts an overlay with the form; Save
         posts to the engine + reloads the grid on success. -->
    <dx-popup [(visible)]="createDialogOpen"
              [width]="480" [height]="520"
              title="New scope grant"
              [showCloseButton]="true"
              [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="create-form">
        <div class="form-row">
          <label>User id *</label>
          <dx-number-box [(value)]="draft.userId" [min]="1"
                         placeholder="numeric app_users.id" [showSpinButtons]="false" />
        </div>

        <div class="form-row">
          <label>Action *</label>
          <dx-select-box [items]="createActions" [(value)]="draft.action"
                         placeholder="Pick action" acceptCustomValue="true" />
        </div>

        <div class="form-row">
          <label>Entity type *</label>
          <dx-select-box [items]="knownEntityTypes" [(value)]="draft.entityType"
                         placeholder="Pick entity type" acceptCustomValue="true" />
        </div>

        <div class="form-row">
          <label>Scope type *</label>
          <dx-select-box [items]="createScopeTypes" [(value)]="draft.scopeType" />
          <small class="hint">Global = every row of this type · Region/Site/Building = hierarchy scope · EntityId = single row</small>
        </div>

        <div class="form-row" *ngIf="draft.scopeType !== 'Global'">
          <label>Scope entity id *</label>
          <dx-text-box [(value)]="draft.scopeEntityId"
                       placeholder="uuid of the region / site / building / entity" />
          <small class="hint">Required for any scope type other than Global.</small>
        </div>

        <div class="form-row">
          <label>Notes</label>
          <dx-text-area [(value)]="draft.notes" [height]="60"
                        placeholder="Optional — e.g. 'temp grant for the Q2 rollout'" />
        </div>

        <div *ngIf="createError" class="create-error">{{ createError }}</div>

        <div class="form-actions">
          <dx-button text="Cancel" (onClick)="closeCreateDialog()" />
          <dx-button text="Save" type="default" (onClick)="submitCreate()"
                     [disabled]="creating" />
        </div>
      </div>
    </dx-popup>

    <!-- Check permission dialog — dry-runs the permission resolver
         without enforcing. Answers "would user X be allowed to do
         action Y on entity Z?" and shows the matching grant id
         when allowed. -->
    <dx-popup [(visible)]="checkDialogOpen"
              [width]="480" [height]="460"
              title="Check permission"
              [showCloseButton]="true"
              [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="create-form">
        <div class="form-row">
          <label>User id *</label>
          <dx-number-box [(value)]="checkDraft.userId" [min]="1"
                         placeholder="numeric app_users.id" [showSpinButtons]="false" />
        </div>
        <div class="form-row">
          <label>Action *</label>
          <dx-select-box [items]="createActions" [(value)]="checkDraft.action"
                         placeholder="Pick action" acceptCustomValue="true" />
        </div>
        <div class="form-row">
          <label>Entity type *</label>
          <dx-select-box [items]="knownEntityTypes" [(value)]="checkDraft.entityType"
                         placeholder="Pick entity type" acceptCustomValue="true" />
        </div>
        <div class="form-row">
          <label>Entity id (optional)</label>
          <dx-text-box [(value)]="checkDraft.entityId"
                       placeholder="Leave blank for type-level check" />
          <small class="hint">Filled in → "can user do X on this specific entity?". Blank → "can user do X on ANY entity of this type?".</small>
        </div>

        <div *ngIf="checkDecision" class="check-result"
             [class.allowed]="checkDecision.allowed"
             [class.denied]="!checkDecision.allowed">
          <strong>{{ checkDecision.allowed ? 'ALLOWED' : 'DENIED' }}</strong>
          <div *ngIf="checkDecision.allowed && checkDecision.matchedGrantId" class="match-line">
            via grant <code>{{ checkDecision.matchedGrantId }}</code>
          </div>
          <div *ngIf="!checkDecision.allowed" class="match-line">
            No matching grant at Global / Region / Site / Building / EntityId scopes.
          </div>
        </div>
        <div *ngIf="checkError" class="create-error">{{ checkError }}</div>

        <div class="form-actions">
          <dx-button text="Close" (onClick)="closeCheckDialog()" />
          <dx-button text="Check" type="default" (onClick)="submitCheck()"
                     [disabled]="checking" />
        </div>
      </div>
    </dx-popup>

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
      <dxi-column caption="" width="90" [allowFiltering]="false" [allowSorting]="false"
                  cellTemplate="actionsTemplate" />

      <div *dxTemplate="let d of 'actionsTemplate'">
        <div class="row-actions">
          <dx-button icon="copy" stylingMode="text" hint="Copy grant id"
                     (onClick)="copyGrantId(d.data)" />
          <dx-button icon="trash" stylingMode="text" hint="Delete this grant"
                     (onClick)="deleteGrant(d.data)" />
        </div>
      </div>
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
    .filter-bar .spacer { flex: 1; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .create-form { display: flex; flex-direction: column; gap: 14px; padding: 4px 2px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .form-row .hint { color: #6b7280; font-size: 11px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
    .create-error { color: #ef4444; font-size: 12px; padding: 6px 8px; background: rgba(239,68,68,0.08); border-radius: 4px; }
    .row-actions { display: flex; gap: 4px; }
    .check-result { padding: 10px 12px; border-radius: 4px; font-size: 13px; }
    .check-result.allowed { background: rgba(34,197,94,0.1); border: 1px solid rgba(34,197,94,0.3); color: #22c55e; }
    .check-result.denied  { background: rgba(239,68,68,0.1); border: 1px solid rgba(239,68,68,0.3); color: #ef4444; }
    .check-result strong { font-size: 15px; letter-spacing: 0.5px; }
    .check-result .match-line { color: #9ca3af; font-size: 11px; margin-top: 4px; word-break: break-all; }
    .check-result .match-line code { background: rgba(148,163,184,0.1); padding: 1px 4px; border-radius: 2px; font-size: 11px; }
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

  /// Create dialog uses the engine's enum-level allow-lists verbatim
  /// (ALLOWED_ACTIONS + ALLOWED_SCOPE_TYPES in scope_grants.rs). The
  /// filter bar's `knownActions` is a broader convenience list.
  createActions    = ['read', 'write', 'delete', 'approve', 'apply'];
  createScopeTypes = ['Global', 'Region', 'Site', 'Building', 'EntityId'];

  userIdFilter = '';
  actionFilter: string | null = null;
  entityTypeFilter: string | null = null;
  grants: ScopeGrant[] = [];
  status = '';

  createDialogOpen = false;
  creating = false;
  createError = '';
  draft: {
    userId: number | null;
    action: string | null;
    entityType: string | null;
    scopeType: string;
    scopeEntityId: string;
    notes: string;
  } = this.emptyDraft();

  checkDialogOpen = false;
  checking = false;
  checkError = '';
  checkDecision: PermissionDecision | null = null;
  checkDraft: {
    userId: number | null;
    action: string | null;
    entityType: string | null;
    entityId: string;
  } = this.emptyCheckDraft();

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

  // ─── Create dialog ─────────────────────────────────────────────────

  private emptyDraft() {
    return {
      userId:        null,
      action:        null,
      entityType:    null,
      scopeType:     'Global',
      scopeEntityId: '',
      notes:         '',
    };
  }

  private emptyCheckDraft() {
    return {
      userId:     null,
      action:     null,
      entityType: null,
      entityId:   '',
    };
  }

  openCreateDialog(): void {
    this.draft = this.emptyDraft();
    this.createError = '';
    this.createDialogOpen = true;
  }

  closeCreateDialog(): void {
    this.createDialogOpen = false;
    this.createError = '';
  }

  /// Client-side validation mirrors the engine's create guard:
  /// positive-int user id, required action + entityType, scopeEntityId
  /// required whenever scopeType != Global. Server still validates
  /// so a stale client can't skip it, but catching locally avoids a
  /// round-trip on obvious mistakes.
  submitCreate(): void {
    const d = this.draft;
    this.createError = '';

    if (!d.userId || d.userId <= 0)              { this.createError = 'User id must be a positive integer.'; return; }
    if (!d.action)                                { this.createError = 'Action is required.'; return; }
    if (!d.entityType)                            { this.createError = 'Entity type is required.'; return; }
    if (!d.scopeType)                             { this.createError = 'Scope type is required.'; return; }
    if (d.scopeType !== 'Global' && !d.scopeEntityId.trim()) {
      this.createError = 'Scope entity id is required for any scope type other than Global.'; return;
    }

    // Guard against typos in optional fields so the engine doesn't
    // reject an otherwise-valid grant for a trimming reason.
    this.creating = true;
    this.engine.createScopeGrant({
      organizationId: environment.defaultTenantId,
      userId:         d.userId,
      action:         d.action,
      entityType:     d.entityType,
      scopeType:      d.scopeType,
      scopeEntityId:  d.scopeType === 'Global' ? undefined : d.scopeEntityId.trim(),
      notes:          d.notes.trim() || undefined,
    }).subscribe({
      next: () => {
        this.creating = false;
        this.createDialogOpen = false;
        this.status = 'Grant created.';
        this.reload();
      },
      error: (err) => {
        this.creating = false;
        // 403 → caller lacks write:ScopeGrant. Surface the distinction
        // because it's a policy issue, not a form issue.
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.createError = 'Forbidden — your user lacks write:ScopeGrant. Ask an admin or use the WPF break-glass path.';
        } else {
          this.createError = err?.error?.detail ?? err?.message ?? 'Create failed.';
        }
      },
    });
  }

  // ─── Row actions ───────────────────────────────────────────────────

  /// Copy the grant uuid to the clipboard. Uses the Clipboard API if
  /// the page is served over HTTPS; falls back to writing into the
  /// status line so the operator can still grab the id by eye. The
  /// async API returns a promise but we don't await — nav away before
  /// the copy lands is acceptable.
  copyGrantId(g: ScopeGrant): void {
    if (!g?.id) return;
    if (typeof navigator !== 'undefined' && navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(g.id).then(
        () => { this.status = `Copied grant id ${g.id}.`; },
        () => { this.status = `Clipboard write failed — id: ${g.id}`; },
      );
    } else {
      this.status = `Grant id: ${g.id}`;
    }
  }

  // ─── Check permission dialog ──────────────────────────────────────

  openCheckDialog(): void {
    this.checkDraft = this.emptyCheckDraft();
    this.checkDecision = null;
    this.checkError = '';
    this.checkDialogOpen = true;
  }

  closeCheckDialog(): void {
    this.checkDialogOpen = false;
    this.checkDecision = null;
    this.checkError = '';
  }

  submitCheck(): void {
    const d = this.checkDraft;
    this.checkError = '';
    this.checkDecision = null;

    if (!d.userId || d.userId <= 0)   { this.checkError = 'User id must be a positive integer.'; return; }
    if (!d.action)                     { this.checkError = 'Action is required.'; return; }
    if (!d.entityType)                 { this.checkError = 'Entity type is required.'; return; }

    this.checking = true;
    this.engine.checkPermission({
      organizationId: environment.defaultTenantId,
      userId:         d.userId,
      action:         d.action,
      entityType:     d.entityType,
      entityId:       d.entityId.trim() || undefined,
    }).subscribe({
      next: (decision) => {
        this.checking = false;
        this.checkDecision = decision;
      },
      error: (err) => {
        this.checking = false;
        this.checkError = err?.error?.detail ?? err?.message ?? 'Check failed.';
      },
    });
  }

  /// Delete a grant. Confirm with a browser prompt + surface the same
  /// two failure classes (403 / other) as create. Reloads the grid
  /// on success so the row disappears.
  deleteGrant(g: ScopeGrant): void {
    if (!g?.id) return;
    if (typeof window !== 'undefined' &&
        !window.confirm(`Delete grant '${g.action}:${g.entityType}' for user ${g.userId}?`)) return;
    this.engine.deleteScopeGrant(g.id, environment.defaultTenantId).subscribe({
      next: () => {
        this.status = 'Grant deleted.';
        this.reload();
      },
      error: (err) => {
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.status = 'Forbidden — your user lacks delete:ScopeGrant on this grant.';
        } else {
          this.status = `Delete failed: ${err?.error?.detail ?? err?.message ?? err}`;
        }
      },
    });
  }
}
