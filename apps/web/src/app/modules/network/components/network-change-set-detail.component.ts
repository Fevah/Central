import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule,
  DxPopupModule, DxSelectBoxModule, DxTextBoxModule, DxTextAreaModule,
  DxNumberBoxModule, DxFormModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ChangeSet, ChangeSetItem,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Detail view of one change-set — header row + item list + action
/// bar. Landed from the change-sets list page (double-click →
/// audit-search drill), OR directly via /network/change-sets/:id.
///
/// Write actions: Submit (Draft → Submitted with required-approvals=1),
/// Apply (Approved → Applied in a transaction), Cancel (any
/// non-terminal → Cancelled). Approve / reject / rollback stay
/// WPF-only for now — need the approver-user selection chrome that
/// the admin-users picker provides on the WPF side.
///
/// Item list shows the before/after JSON snapshots inline so an
/// operator can scan a Set's full payload without drilling into
/// every entity. Pretty-prints via JSON.stringify with 2-space
/// indent; truncation is manual (scroll the code block) rather than
/// server-side.
@Component({
  selector: 'app-network-change-set-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule,
            DxPopupModule, DxSelectBoxModule, DxTextBoxModule,
            DxTextAreaModule, DxNumberBoxModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/change-sets" class="back-link">← Change sets</a>
      <h2>Change set detail</h2>
      <small *ngIf="set" class="subtitle">
        <span [class]="'badge badge-' + statusBadgeClass(set.status)">{{ set.status }}</span>
        · {{ set.title }}
      </small>
    </div>

    <!-- Action bar — buttons enabled based on current status. Draft
         → Submit; Approved → Apply; !terminal → Cancel. -->
    <div *ngIf="set" class="action-bar">
      <dx-button text="Submit for approval" icon="export" type="default"
                 stylingMode="contained"
                 [disabled]="busy || set.status !== 'Draft'"
                 hint="Submit this Draft Set for approval (sets required approvals to 1)."
                 (onClick)="onSubmit()" />
      <dx-button text="Approve" icon="check" type="success"
                 stylingMode="contained"
                 [disabled]="busy || set.status !== 'Submitted'"
                 hint="Record an Approve decision. May move the Set to Approved if approvals are satisfied."
                 (onClick)="onApprove()" />
      <dx-button text="Reject" icon="close" type="danger"
                 stylingMode="contained"
                 [disabled]="busy || set.status !== 'Submitted'"
                 hint="Record a Reject decision. Moves the Set to Rejected — terminal."
                 (onClick)="onReject()" />
      <dx-button text="Apply now" icon="save" type="default"
                 [disabled]="busy || set.status !== 'Approved'"
                 hint="Run every item in order. Transactional — partial apply rolls back on error."
                 (onClick)="onApply()" />
      <dx-button text="Cancel set" icon="close" stylingMode="outlined"
                 [disabled]="busy || isTerminal(set.status)"
                 hint="Cancel this Set. Terminal — can't be reopened."
                 (onClick)="onCancel()" />
      <dx-button text="Roll back" icon="undo" stylingMode="outlined"
                 [disabled]="busy || set.status !== 'Applied'"
                 hint="Reverse every item's mutation in reverse order. Moves the Set to RolledBack — terminal."
                 (onClick)="onRollback()" />
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

    <div class="items-header">
      <h3 class="section-title">Items</h3>
      <dx-button *ngIf="set && set.status === 'Draft'"
                 text="Add item" icon="add" stylingMode="outlined"
                 (onClick)="openItemDialog()" [disabled]="busy" />
    </div>
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
      <dxi-column caption="" [allowSorting]="false" [allowFiltering]="false"
                  [allowHeaderFiltering]="false" [allowGrouping]="false"
                  [allowSearch]="false" width="90"
                  cellTemplate="removeTemplate" />

      <div *dxTemplate="let d of 'removeTemplate'">
        <dx-button *ngIf="set?.status === 'Draft'"
                   text="Remove" icon="trash" stylingMode="text" type="danger"
                   [disabled]="busy"
                   (onClick)="removeItem(d.data)" />
      </div>

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

    <!-- Add-item dialog — only reachable when the Set is Draft.
         Captures one item + appends to the Set; operator runs it
         again for each item. beforeJson + afterJson are free-form
         JSON text areas because the engine stores them as jsonb
         without schema validation. -->
    <dx-popup [(visible)]="itemDialogOpen"
              [width]="640" [height]="640"
              title="Add change-set item"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>Entity type *</label>
          <dx-select-box [items]="itemEntityTypes" [(value)]="itemDraft.entityType"
                         acceptCustomValue="true" placeholder="Device / Vlan / Subnet / ..." />
        </div>
        <div class="form-row">
          <label>Action *</label>
          <dx-select-box [items]="itemActions" [(value)]="itemDraft.action" />
        </div>
        <div class="form-row" *ngIf="itemDraft.action !== 'Create'">
          <label>Entity id *</label>
          <dx-text-box [(value)]="itemDraft.entityId"
                       placeholder="uuid of the existing row" />
          <small class="hint">Required for Update / Delete / Rename. Blank allowed only for Create.</small>
        </div>
        <div class="form-row" *ngIf="itemDraft.action === 'Update' || itemDraft.action === 'Rename'">
          <label>Expected version</label>
          <dx-number-box [(value)]="itemDraft.expectedVersion" [showSpinButtons]="false"
                         placeholder="optional — enables stale-version guard" />
        </div>
        <div class="form-row" *ngIf="itemDraft.action !== 'Create'">
          <label>Before JSON</label>
          <dx-text-area [(value)]="itemDraft.beforeJsonText" [height]="100"
                        placeholder='{ "hostname": "mep-91-sw01-old" }' />
        </div>
        <div class="form-row" *ngIf="itemDraft.action !== 'Delete'">
          <label>After JSON</label>
          <dx-text-area [(value)]="itemDraft.afterJsonText" [height]="100"
                        placeholder='{ "hostname": "mep-91-sw01" }' />
        </div>
        <div class="form-row">
          <label>Notes</label>
          <dx-text-area [(value)]="itemDraft.notes" [height]="60" />
        </div>
        <div *ngIf="itemError" class="form-error">{{ itemError }}</div>
        <div class="form-actions">
          <dx-button text="Cancel" (onClick)="closeItemDialog()" />
          <dx-button text="Add" type="default" (onClick)="submitItem()"
                     [disabled]="busy" />
        </div>
      </div>
    </dx-popup>
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
    .action-bar { display: flex; gap: 8px; margin: 10px 0; }
    .items-header { display: flex; justify-content: space-between; align-items: center; margin-top: 20px; }
    .items-header .section-title { margin: 0; }
    .form { display: flex; flex-direction: column; gap: 14px; padding: 4px 2px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .form-row .hint { color: #6b7280; font-size: 11px; }
    .form-error { color: #ef4444; font-size: 12px; padding: 6px 8px; background: rgba(239,68,68,0.08); border-radius: 4px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
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
  busy = false;

  /// Common entity types the engine's action dispatcher knows about.
  /// acceptCustomValue on the combo lets operators type a less-common
  /// one (e.g. DhcpRelayTarget) without the UI blocking them.
  itemEntityTypes = ['Device', 'Vlan', 'Subnet', 'Server', 'Link',
                     'Port', 'DhcpRelayTarget', 'ScopeGrant'];
  itemActions = ['Create', 'Update', 'Delete', 'Rename'];

  itemDialogOpen = false;
  itemError = '';
  itemDraft: {
    entityType: string | null;
    action: string;
    entityId: string;
    expectedVersion: number | null;
    beforeJsonText: string;
    afterJsonText: string;
    notes: string;
  } = this.emptyItemDraft();

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

  /// Terminal lifecycle states — matches `ChangeSetStatus::is_terminal`
  /// in the engine. Cancel button is disabled on terminal states.
  isTerminal(status: string): boolean {
    return status === 'Rejected' || status === 'Applied' || status === 'Cancelled';
  }

  // ─── Write actions ────────────────────────────────────────────────

  /// Submit Draft for approval. Fixed required-approvals=1 for this
  /// surface; multi-approval sets run via the WPF client where the
  /// approver-user picker is already wired.
  onSubmit(): void {
    if (!this.set || this.busy) return;
    this.runAction(
      this.engine.submitChangeSet(this.set.id, environment.defaultTenantId, 1),
      'Submitted for approval.');
  }

  /// Apply Approved set. Server runs every item in item_order in one
  /// transaction; partial apply on error rolls back.
  onApply(): void {
    if (!this.set || this.busy) return;
    if (typeof window !== 'undefined' &&
        !window.confirm(`Apply change set '${this.set.title}' now? This runs every item in sequence.`)) return;
    this.runAction(
      this.engine.applyChangeSet(this.set.id, environment.defaultTenantId),
      'Applied.');
  }

  /// Record an Approve decision. Prompt for optional notes + an
  /// approver display name. The engine may transition the Set to
  /// Approved if required-approvals is reached; reload the Set
  /// after the call to pick up the new status.
  onApprove(): void {
    this.recordDecision('Approve');
  }

  /// Record a Reject decision. Moves the Set to Rejected (terminal).
  onReject(): void {
    this.recordDecision('Reject');
  }

  private recordDecision(decision: 'Approve' | 'Reject'): void {
    if (!this.set || this.busy) return;
    const notes = typeof window !== 'undefined'
      ? window.prompt(`Optional note for the ${decision} decision:`, '') ?? undefined
      : undefined;
    if (typeof window !== 'undefined' &&
        !window.confirm(`${decision} change set '${this.set.title}'?`)) return;

    this.busy = true;
    this.status = 'Working…';
    this.engine.recordChangeSetDecision(this.set.id, environment.defaultTenantId, {
      decision,
      notes: notes || undefined,
    }).subscribe({
      next: () => {
        this.busy = false;
        this.status = `${decision} recorded — reloading.`;
        // Decision endpoint returns an approval + change-set + items
        // envelope, not just the Set row; easier to just refetch the
        // canonical Set for a clean state update.
        this.ngOnInit();
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.status = 'Forbidden — your user lacks approve/reject:ChangeSet.';
        } else if (status === 409) {
          this.status = `Illegal state transition: ${err?.error?.detail ?? 'only Submitted sets accept decisions'}.`;
        } else {
          this.status = `Failed: ${err?.error?.detail ?? err?.message ?? err}`;
        }
      },
    });
  }

  /// Roll back an Applied Set. Reverse-order execution of each
  /// item's reverse mutation. Same confirmation pattern as apply.
  onRollback(): void {
    if (!this.set || this.busy) return;
    if (typeof window !== 'undefined' &&
        !window.confirm(`Roll back change set '${this.set.title}'? Each item's mutation will be reversed.`)) return;
    this.runAction(
      this.engine.rollbackChangeSet(this.set.id, environment.defaultTenantId),
      'Rolled back.');
  }

  /// Cancel set. Prompt for optional note so audit carries the
  /// reason. Terminal afterwards.
  onCancel(): void {
    if (!this.set || this.busy) return;
    const notes = typeof window !== 'undefined'
      ? window.prompt('Cancellation note (optional):', '') ?? undefined
      : undefined;
    if (typeof window !== 'undefined' &&
        !window.confirm(`Cancel change set '${this.set.title}'? Can't be reopened.`)) return;
    this.runAction(
      this.engine.cancelChangeSet(this.set.id, environment.defaultTenantId, notes || undefined),
      'Cancelled.');
  }

  // ─── Add-item dialog ───────────────────────────────────────────

  private emptyItemDraft() {
    return {
      entityType:      null,
      action:          'Create',
      entityId:        '',
      expectedVersion: null,
      beforeJsonText:  '',
      afterJsonText:   '',
      notes:           '',
    };
  }

  openItemDialog(): void {
    this.itemDraft = this.emptyItemDraft();
    this.itemError = '';
    this.itemDialogOpen = true;
  }

  closeItemDialog(): void {
    this.itemDialogOpen = false;
    this.itemError = '';
  }

  submitItem(): void {
    if (!this.set) return;
    const d = this.itemDraft;
    this.itemError = '';

    if (!d.entityType)           { this.itemError = 'Entity type is required.'; return; }
    if (!d.action)               { this.itemError = 'Action is required.'; return; }
    if (d.action !== 'Create' && !d.entityId.trim()) {
      this.itemError = 'Entity id is required for Update / Delete / Rename.';
      return;
    }

    // Parse JSON text areas — empty string → undefined, bad JSON →
    // form error.
    let beforeJson: unknown;
    let afterJson: unknown;
    try {
      beforeJson = d.beforeJsonText.trim() ? JSON.parse(d.beforeJsonText) : undefined;
    } catch { this.itemError = 'Before JSON is not valid JSON.'; return; }
    try {
      afterJson  = d.afterJsonText.trim()  ? JSON.parse(d.afterJsonText)  : undefined;
    } catch { this.itemError = 'After JSON is not valid JSON.'; return; }

    this.busy = true;
    this.engine.addChangeSetItem(this.set.id, environment.defaultTenantId, {
      entityType:      d.entityType,
      entityId:        d.entityId.trim() || null,
      action:          d.action,
      beforeJson,
      afterJson,
      expectedVersion: d.expectedVersion,
      notes:           d.notes.trim() || null,
    }).subscribe({
      next: (item) => {
        this.busy = false;
        this.itemDialogOpen = false;
        this.items = [...this.items, item];
        // Bump the item count on the cached Set so the meta grid
        // shows the new total without a full reload.
        if (this.set) this.set = { ...this.set, itemCount: this.set.itemCount + 1 };
        this.status = 'Item added.';
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.itemError = 'Forbidden — your user lacks write:ChangeSet.';
        } else if (status === 409) {
          this.itemError = 'Can only add items to a Draft set.';
        } else {
          this.itemError = err?.error?.detail ?? err?.message ?? 'Add failed.';
        }
      },
    });
  }

  /// Soft-delete a pending item from a Draft Set. Optimistic UI —
  /// strips from the local items list + decrements itemCount before
  /// the engine confirms; a failure reloads the full detail so
  /// state stays truthful. Disabled by the template when the Set
  /// isn't Draft (engine rejects non-Draft deletes too, so this is
  /// just belt-and-braces).
  removeItem(item: ChangeSetItem): void {
    if (!this.set || this.set.status !== 'Draft') return;
    if (!confirm(`Remove item #${item.itemOrder} (${item.action} ${item.entityType})?`)) return;

    this.busy = true;
    this.status = 'Removing…';
    const setId = this.set.id;
    this.engine.deleteChangeSetItem(setId, item.id, environment.defaultTenantId).subscribe({
      next: () => {
        this.busy = false;
        this.items = this.items.filter(i => i.id !== item.id);
        if (this.set) this.set = { ...this.set, itemCount: Math.max(0, this.set.itemCount - 1) };
        this.status = 'Item removed.';
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.status = 'Forbidden — your user lacks write:ChangeSet.';
        } else if (status === 400 || status === 409) {
          this.status = `Illegal: ${err?.error?.detail ?? 'item not in Draft Set'}.`;
        } else {
          this.status = `Remove failed: ${err?.error?.detail ?? err?.message ?? err}`;
        }
        // Reload to resync in case the delete partially landed.
        this.ngOnInit();
      },
    });
  }

  /// Shared success/failure handler. 403 surfaces specifically so
  /// operators see "your user lacks write:ChangeSet" rather than a
  /// generic error. 409 is the engine's "illegal state transition"
  /// response.
  private runAction(obs: import('rxjs').Observable<ChangeSet>, successMsg: string): void {
    this.busy = true;
    this.status = 'Working…';
    obs.subscribe({
      next: (updated) => {
        this.busy = false;
        this.set = updated;
        this.status = successMsg;
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.status = 'Forbidden — your user lacks the required permission on ChangeSet.';
        } else if (status === 409) {
          this.status = `Illegal state transition: ${err?.error?.detail ?? 'current status doesn\\'t allow that action'}.`;
        } else {
          this.status = `Failed: ${err?.error?.detail ?? err?.message ?? err}`;
        }
      },
    });
  }
}
