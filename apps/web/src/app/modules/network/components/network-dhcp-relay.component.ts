import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
  DxPopupModule, DxNumberBoxModule, DxTextBoxModule, DxTextAreaModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, DhcpRelayTargetRow, VlanListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web grid over /api/net/dhcp-relay-targets. Displays every
/// (vlan × server_ip × priority) tuple in the tenant, with an
/// optional VLAN filter driven by the engine's thin VLAN list.
/// Grouping by vlanTag is the natural shape for operator review
/// since a single VLAN typically carries two relay targets (the
/// primary + the peer).
///
/// Read-only for this slice — writes stay on the WPF BulkPanel via
/// CSV + the edit-in-place API. A popup create form can land in a
/// follow-up once the hostname → vlanUuid resolution story is
/// settled (picker + auto-complete from the VLAN thin list).
@Component({
  selector: 'app-network-dhcp-relay',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule,
            DxPopupModule, DxNumberBoxModule, DxTextBoxModule, DxTextAreaModule],
  template: `
    <div class="page-header">
      <h2>DHCP relay targets</h2>
      <small class="subtitle">net.dhcp_relay_target rows — M:N (vlan × server_ip) with priority ordering. Rendered into every device's <code>set system dhcp-relay server</code> stanza.</small>
    </div>

    <div class="filter-bar">
      <label>VLAN</label>
      <dx-select-box class="md" [items]="vlans" [(value)]="selectedVlanId"
                     valueExpr="id" displayExpr="displayLabel"
                     [showClearButton]="true"
                     placeholder="(all VLANs)"
                     (onValueChanged)="reload()" />

      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>

      <span class="spacer"></span>

      <dx-button text="New target" icon="add" type="default"
                 stylingMode="contained" (onClick)="openCreateDialog()" />
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="vlanLabel"   caption="VLAN"     width="180" [groupIndex]="0" />
      <dxi-column dataField="serverIp"    caption="Server IP" width="160" [fixed]="true" />
      <dxi-column dataField="priority"    caption="Priority"  width="90"  dataType="number" sortOrder="asc" />
      <dxi-column dataField="status"      caption="Status"    width="90" />
      <dxi-column dataField="version"     caption="v"         width="50"  dataType="number" />
      <dxi-column dataField="notes"       caption="Notes" />
      <dxi-column dataField="createdAt"   caption="Created"   dataType="datetime" width="170"
                  format="yyyy-MM-dd HH:mm" />
      <dxi-column dataField="id"          caption="UUID"      width="260" />
      <dxi-column caption="" width="100" [allowFiltering]="false" [allowSorting]="false"
                  cellTemplate="actionsTemplate" />

      <div *dxTemplate="let d of 'actionsTemplate'">
        <div class="row-actions">
          <dx-button icon="edit" stylingMode="text" hint="Edit priority + notes"
                     (onClick)="openEditDialog(d.data)" />
          <dx-button icon="trash" stylingMode="text" hint="Delete this target"
                     (onClick)="deleteRow(d.data)" />
        </div>
      </div>
    </dx-data-grid>

    <!-- Create dialog — VLAN picker + server IP + priority + notes -->
    <dx-popup [(visible)]="createDialogOpen"
              [width]="460" [height]="480"
              title="New DHCP relay target"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>VLAN *</label>
          <dx-select-box [items]="vlans" [(value)]="createDraft.vlanId"
                         valueExpr="id" displayExpr="displayLabel"
                         [searchEnabled]="true" placeholder="Pick a VLAN" />
        </div>
        <div class="form-row">
          <label>Server IP *</label>
          <dx-text-box [(value)]="createDraft.serverIp"
                       placeholder="10.11.120.10" />
          <small class="hint">Bare host address (no prefix). Server casts to inet on insert.</small>
        </div>
        <div class="form-row">
          <label>Priority</label>
          <dx-number-box [(value)]="createDraft.priority" [min]="0"
                         [showSpinButtons]="true" />
          <small class="hint">Default 10. Lower runs earlier — primary / peer pair typically use 10 + 20.</small>
        </div>
        <div class="form-row">
          <label>Notes</label>
          <dx-text-area [(value)]="createDraft.notes" [height]="60" />
        </div>
        <div *ngIf="formError" class="form-error">{{ formError }}</div>
        <div class="form-actions">
          <dx-button text="Cancel" (onClick)="closeCreateDialog()" />
          <dx-button text="Save" type="default" (onClick)="submitCreate()"
                     [disabled]="busy" />
        </div>
      </div>
    </dx-popup>

    <!-- Edit dialog — priority + notes only. serverIp + vlan are
         immutable (the unique key of the row); to move to a different
         VLAN / IP, delete + recreate. -->
    <dx-popup [(visible)]="editDialogOpen"
              [width]="460" [height]="400"
              title="Edit DHCP relay target"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>VLAN</label>
          <span class="readonly">{{ editTarget?.vlanLabel }}</span>
        </div>
        <div class="form-row">
          <label>Server IP</label>
          <span class="readonly">{{ editTarget?.serverIp }}</span>
        </div>
        <div class="form-row">
          <label>Priority</label>
          <dx-number-box [(value)]="editDraft.priority" [min]="0"
                         [showSpinButtons]="true" />
        </div>
        <div class="form-row">
          <label>Notes</label>
          <dx-text-area [(value)]="editDraft.notes" [height]="60" />
        </div>
        <div *ngIf="formError" class="form-error">{{ formError }}</div>
        <div class="form-actions">
          <dx-button text="Cancel" (onClick)="closeEditDialog()" />
          <dx-button text="Save" type="default" (onClick)="submitEdit()"
                     [disabled]="busy" />
        </div>
      </div>
    </dx-popup>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .subtitle code { background: #1e293b; padding: 1px 6px; border-radius: 3px; font-size: 11px; }
    .filter-bar { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .md { width: 260px; }
    .filter-bar .spacer { flex: 1; min-width: 12px; }
    .status-line { color: #666; font-size: 12px; }
    .row-actions { display: flex; gap: 4px; }
    .form { display: flex; flex-direction: column; gap: 14px; padding: 4px 2px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .form-row .hint { color: #6b7280; font-size: 11px; }
    .form-row .readonly { color: #cbd5e1; padding: 6px 8px; background: #0f172a; border-radius: 4px; font-size: 13px; }
    .form-error { color: #ef4444; font-size: 12px; padding: 6px 8px; background: rgba(239,68,68,0.08); border-radius: 4px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
  `]
})
export class NetworkDhcpRelayComponent implements OnInit {
  /// Decorated VLAN list for the filter combo — the thin list
  /// returns id + vlan_id + display_name; the combo needs a
  /// human-readable `displayLabel` pre-computed.
  vlans: Array<VlanListRow & { displayLabel: string }> = [];
  selectedVlanId: string | null = null;

  /// Rows decorated with a `vlanLabel` projection so the group panel
  /// shows "VLAN 120 (Servers)" rather than a raw uuid.
  rows: Array<DhcpRelayTargetRow & { vlanLabel: string }> = [];

  loading = false;
  busy = false;
  status = '';

  createDialogOpen = false;
  editDialogOpen = false;
  formError = '';
  editTarget: (DhcpRelayTargetRow & { vlanLabel: string }) | null = null;
  createDraft: { vlanId: string | null; serverIp: string; priority: number; notes: string } = {
    vlanId: null, serverIp: '', priority: 10, notes: '',
  };
  editDraft: { priority: number; notes: string } = { priority: 10, notes: '' };

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    // Load VLANs first so the filter combo + row labels have a
    // resolved uuid → tag mapping before the row query completes.
    // Both reloads go through reload() once the VLANs are in.
    this.engine.listVlans(environment.defaultTenantId).subscribe({
      next: (vs) => {
        this.vlans = vs.map(v => ({
          ...v,
          displayLabel: `VLAN ${v.vlanId} · ${v.displayName}`,
        }));
        this.reload();
      },
      error: () => {
        this.vlans = [];
        this.reload();   // still attempt the row load — raw uuids is better than nothing
      },
    });
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine
      .listDhcpRelayTargets(environment.defaultTenantId, this.selectedVlanId ?? undefined)
      .subscribe({
        next: (rows) => {
          const vlanLookup = new Map<string, { vlanId: number; displayName: string }>();
          for (const v of this.vlans) {
            vlanLookup.set(v.id, { vlanId: v.vlanId, displayName: v.displayName });
          }
          this.rows = rows.map(r => {
            const v = vlanLookup.get(r.vlanId);
            return {
              ...r,
              vlanLabel: v ? `VLAN ${v.vlanId} · ${v.displayName}` : r.vlanId,
            };
          });
          this.loading = false;
          this.status = `${rows.length} relay target${rows.length === 1 ? '' : 's'}`;
        },
        error: (err) => {
          this.loading = false;
          this.status = err?.status === 403
            ? 'Forbidden — your user lacks read:DhcpRelayTarget.'
            : `Load failed: ${err?.message ?? err}`;
          this.rows = [];
        },
      });
  }

  /// Double-click → relay detail page (Summary + Audit). Audit is
  /// a tab-click away inside the detail page; matches the pattern
  /// used by the other entity grids.
  onRowDoubleClick(e: { data: DhcpRelayTargetRow }): void {
    const r = e?.data;
    if (!r?.id) return;
    this.router.navigate(['/network/net-dhcp-relay', r.id]);
  }

  // ─── Create dialog ────────────────────────────────────────────────

  openCreateDialog(): void {
    this.createDraft = {
      vlanId:   this.selectedVlanId,   // seed with the filter's VLAN if set
      serverIp: '',
      priority: 10,
      notes:    '',
    };
    this.formError = '';
    this.createDialogOpen = true;
  }

  closeCreateDialog(): void {
    this.createDialogOpen = false;
    this.formError = '';
  }

  submitCreate(): void {
    const d = this.createDraft;
    this.formError = '';
    if (!d.vlanId) { this.formError = 'VLAN is required.'; return; }
    if (!d.serverIp.trim()) { this.formError = 'Server IP is required.'; return; }
    if (d.priority < 0) { this.formError = 'Priority must be non-negative.'; return; }

    this.busy = true;
    this.engine.createDhcpRelayTarget({
      organizationId: environment.defaultTenantId,
      vlanId:   d.vlanId,
      serverIp: d.serverIp.trim(),
      priority: d.priority,
      notes:    d.notes.trim() || null,
    }).subscribe({
      next: () => {
        this.busy = false;
        this.createDialogOpen = false;
        this.status = 'Target created.';
        this.reload();
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.formError = 'Forbidden — your user lacks write:DhcpRelayTarget.';
        } else if (status === 409) {
          this.formError = 'A target already exists for this VLAN + server IP.';
        } else {
          this.formError = err?.error?.detail ?? err?.message ?? 'Create failed.';
        }
      },
    });
  }

  // ─── Edit dialog ──────────────────────────────────────────────────

  openEditDialog(row: DhcpRelayTargetRow & { vlanLabel: string }): void {
    this.editTarget = row;
    this.editDraft = {
      priority: row.priority,
      notes:    row.notes ?? '',
    };
    this.formError = '';
    this.editDialogOpen = true;
  }

  closeEditDialog(): void {
    this.editDialogOpen = false;
    this.editTarget = null;
    this.formError = '';
  }

  submitEdit(): void {
    if (!this.editTarget) return;
    const d = this.editDraft;
    if (d.priority < 0) { this.formError = 'Priority must be non-negative.'; return; }

    this.busy = true;
    this.engine.updateDhcpRelayTarget(this.editTarget.id, {
      organizationId: environment.defaultTenantId,
      priority: d.priority,
      notes:    d.notes.trim() || null,
      version:  this.editTarget.version,
    }).subscribe({
      next: () => {
        this.busy = false;
        this.editDialogOpen = false;
        this.editTarget = null;
        this.status = 'Target updated.';
        this.reload();
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.formError = 'Forbidden — lacks write:DhcpRelayTarget.';
        } else if (status === 412) {
          this.formError = 'Row changed since load — refresh + retry.';
        } else {
          this.formError = err?.error?.detail ?? err?.message ?? 'Update failed.';
        }
      },
    });
  }

  // ─── Delete ───────────────────────────────────────────────────────

  deleteRow(row: DhcpRelayTargetRow & { vlanLabel: string }): void {
    if (!row?.id) return;
    if (typeof window !== 'undefined' &&
        !window.confirm(`Delete target ${row.vlanLabel} → ${row.serverIp}?`)) return;
    this.engine.deleteDhcpRelayTarget(row.id, environment.defaultTenantId).subscribe({
      next: () => {
        this.status = 'Target deleted.';
        this.reload();
      },
      error: (err) => {
        this.status = err?.status === 403
          ? 'Forbidden — lacks delete:DhcpRelayTarget.'
          : `Delete failed: ${err?.error?.detail ?? err?.message ?? err}`;
      },
    });
  }
}
