import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
  DxPopupModule, DxTextBoxModule, DxTextAreaModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, NamingOverride,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Admin-facing CRUD for naming template overrides. Complements the
/// `/network/naming-preview` dry-run page: operators prototype a
/// template there, then persist it here at the right scope.
///
/// Scope model (same as the engine's resolver):
///   Global   — applies to every matching entity_type in the tenant
///   Region   — applies to devices/links/servers inside that region
///   Site     — applies to devices/links/servers inside that site
///   Building — applies to devices/links/servers inside that building
///
/// subtypeCode narrows further — for Device that's the role_code
/// (Core / L1Core / Access), for Link it's type_code, for Server
/// it's profile_code. Null means "any subtype at this scope".
@Component({
  selector: 'app-network-naming-overrides',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule,
            DxPopupModule, DxTextBoxModule, DxTextAreaModule],
  template: `
    <div class="page-header">
      <h2>Naming overrides</h2>
      <small class="subtitle">Persisted scope-aware overrides for device / link / server naming templates. Dry-run new templates on the <a routerLink="/network/naming-preview">preview page</a> before saving here.</small>
    </div>

    <div class="filter-bar">
      <label>Entity type</label>
      <dx-select-box class="md" [items]="entityTypes" [(value)]="entityTypeFilter"
                     [showClearButton]="true" placeholder="(all)"
                     (onValueChanged)="reload()" />
      <label>Scope</label>
      <dx-select-box class="md" [items]="scopeLevels" [(value)]="scopeLevelFilter"
                     [showClearButton]="true" placeholder="(all)"
                     (onValueChanged)="reload()" />

      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>

      <span class="spacer"></span>

      <dx-button text="New override" icon="add" type="default"
                 stylingMode="contained" (onClick)="openCreateDialog()" />
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }">
      <dxi-column dataField="entityType"     caption="Entity"   width="120" [groupIndex]="0" />
      <dxi-column dataField="scopeLevel"     caption="Scope"    width="100" [groupIndex]="1" />
      <dxi-column dataField="subtypeCode"    caption="Subtype"  width="130" />
      <dxi-column dataField="scopeEntityId"  caption="Scope id" width="260" />
      <dxi-column dataField="namingTemplate" caption="Template" [fixed]="true" width="260" />
      <dxi-column dataField="notes"          caption="Notes" />
      <dxi-column dataField="status"         caption="Status"   width="90" />
      <dxi-column dataField="updatedAt"      caption="Updated"  width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" sortOrder="desc" [sortIndex]="0" />
      <dxi-column caption="" width="100" [allowFiltering]="false" [allowSorting]="false"
                  cellTemplate="actionsTemplate" />

      <div *dxTemplate="let d of 'actionsTemplate'">
        <div class="row-actions">
          <dx-button icon="edit" stylingMode="text" hint="Edit template + notes"
                     (onClick)="openEditDialog(d.data)" />
          <dx-button icon="trash" stylingMode="text" hint="Delete this override"
                     (onClick)="deleteRow(d.data)" />
        </div>
      </div>
    </dx-data-grid>

    <!-- Create dialog -->
    <dx-popup [(visible)]="createDialogOpen"
              [width]="520" [height]="560"
              title="New naming override"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>Entity type *</label>
          <dx-select-box [items]="entityTypes" [(value)]="createDraft.entityType"
                         placeholder="Device / Link / Server" />
        </div>
        <div class="form-row">
          <label>Subtype (optional)</label>
          <dx-text-box [(value)]="createDraft.subtypeCode"
                       placeholder="role_code / type_code / profile_code" />
          <small class="hint">Blank means "any subtype at this scope".</small>
        </div>
        <div class="form-row">
          <label>Scope *</label>
          <dx-select-box [items]="scopeLevels" [(value)]="createDraft.scopeLevel" />
        </div>
        <div class="form-row" *ngIf="createDraft.scopeLevel !== 'Global'">
          <label>Scope entity id *</label>
          <dx-text-box [(value)]="createDraft.scopeEntityId"
                       placeholder="uuid of the region / site / building" />
          <small class="hint">Required for any scope other than Global.</small>
        </div>
        <div class="form-row">
          <label>Template *</label>
          <dx-text-box [(value)]="createDraft.namingTemplate"
                       placeholder="e.g. {building_code}-{role_code}{instance}" />
          <small class="hint">Preview the template first on the <a routerLink="/network/naming-preview">naming preview</a> page.</small>
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

    <!-- Edit dialog — entity_type + scope_level + scope_entity_id are
         immutable (the uniqueness key of the override). Only the
         template + notes are editable. -->
    <dx-popup [(visible)]="editDialogOpen"
              [width]="520" [height]="460"
              title="Edit naming override"
              [showCloseButton]="true" [dragEnabled]="true">
      <div *dxTemplate="let d of 'content'" class="form">
        <div class="form-row">
          <label>Entity / scope</label>
          <span class="readonly">{{ editTarget?.entityType }} · {{ editTarget?.scopeLevel }} · {{ editTarget?.subtypeCode ?? '(any subtype)' }}</span>
        </div>
        <div class="form-row">
          <label>Template *</label>
          <dx-text-box [(value)]="editDraft.namingTemplate" />
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
    .subtitle a { color: #60a5fa; }
    .filter-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 10px; }
    .filter-bar label { color: #888; font-size: 12px; margin-right: -4px; }
    .filter-bar .md { width: 160px; }
    .filter-bar .spacer { flex: 1; min-width: 12px; }
    .status-line { color: #666; font-size: 12px; }
    .row-actions { display: flex; gap: 4px; }
    .form { display: flex; flex-direction: column; gap: 14px; padding: 4px 2px; }
    .form-row { display: flex; flex-direction: column; gap: 4px; }
    .form-row label { color: #9ca3af; font-size: 12px; }
    .form-row .hint { color: #6b7280; font-size: 11px; }
    .form-row .hint a { color: #60a5fa; }
    .form-row .readonly { color: #cbd5e1; padding: 6px 8px; background: #0f172a; border-radius: 4px; font-size: 13px; }
    .form-error { color: #ef4444; font-size: 12px; padding: 6px 8px; background: rgba(239,68,68,0.08); border-radius: 4px; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
  `]
})
export class NetworkNamingOverridesComponent implements OnInit {
  /// Entity types that carry a naming_template on their profile row
  /// (device_role / link_type / server_profile). Must match the
  /// engine's resolver or saves fall through to the default.
  entityTypes = ['Device', 'Link', 'Server'];
  scopeLevels = ['Global', 'Region', 'Site', 'Building'];

  entityTypeFilter: string | null = null;
  scopeLevelFilter: string | null = null;
  rows: NamingOverride[] = [];
  loading = false;
  busy = false;
  status = '';

  createDialogOpen = false;
  editDialogOpen = false;
  editTarget: NamingOverride | null = null;
  formError = '';
  createDraft: {
    entityType: string | null;
    subtypeCode: string;
    scopeLevel: string;
    scopeEntityId: string;
    namingTemplate: string;
    notes: string;
  } = this.emptyCreateDraft();
  editDraft: { namingTemplate: string; notes: string } = { namingTemplate: '', notes: '' };

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listNamingOverrides(
      environment.defaultTenantId,
      this.entityTypeFilter ?? undefined,
      this.scopeLevelFilter ?? undefined,
    ).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} override${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  private emptyCreateDraft() {
    return {
      entityType:     null,
      subtypeCode:    '',
      scopeLevel:     'Global',
      scopeEntityId:  '',
      namingTemplate: '',
      notes:          '',
    };
  }

  openCreateDialog(): void {
    this.createDraft = this.emptyCreateDraft();
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
    if (!d.entityType)              { this.formError = 'Entity type is required.'; return; }
    if (!d.scopeLevel)              { this.formError = 'Scope is required.'; return; }
    if (d.scopeLevel !== 'Global' && !d.scopeEntityId.trim()) {
      this.formError = 'Scope entity id is required for any scope other than Global.';
      return;
    }
    if (!d.namingTemplate.trim())   { this.formError = 'Template is required.'; return; }

    this.busy = true;
    this.engine.createNamingOverride({
      organizationId: environment.defaultTenantId,
      entityType:     d.entityType,
      subtypeCode:    d.subtypeCode.trim() || null,
      scopeLevel:     d.scopeLevel,
      scopeEntityId:  d.scopeLevel === 'Global' ? null : d.scopeEntityId.trim(),
      namingTemplate: d.namingTemplate.trim(),
      notes:          d.notes.trim() || null,
    }).subscribe({
      next: () => {
        this.busy = false;
        this.createDialogOpen = false;
        this.status = 'Override created.';
        this.reload();
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.formError = 'Forbidden — your user lacks write:NamingTemplateOverride.';
        } else if (status === 409) {
          this.formError = 'An override already exists for this entity/subtype/scope tuple.';
        } else {
          this.formError = err?.error?.detail ?? err?.message ?? 'Create failed.';
        }
      },
    });
  }

  openEditDialog(row: NamingOverride): void {
    this.editTarget = row;
    this.editDraft = {
      namingTemplate: row.namingTemplate,
      notes:          row.notes ?? '',
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
    if (!d.namingTemplate.trim()) { this.formError = 'Template is required.'; return; }

    this.busy = true;
    this.engine.updateNamingOverride(this.editTarget.id, {
      organizationId: environment.defaultTenantId,
      namingTemplate: d.namingTemplate.trim(),
      notes:          d.notes.trim() || null,
      version:        this.editTarget.version,
    }).subscribe({
      next: () => {
        this.busy = false;
        this.editDialogOpen = false;
        this.editTarget = null;
        this.status = 'Override updated.';
        this.reload();
      },
      error: (err) => {
        this.busy = false;
        const status = err?.status as number | undefined;
        if (status === 403) {
          this.formError = 'Forbidden — lacks write:NamingTemplateOverride.';
        } else if (status === 412) {
          this.formError = 'Override changed since load — refresh + retry.';
        } else {
          this.formError = err?.error?.detail ?? err?.message ?? 'Update failed.';
        }
      },
    });
  }

  deleteRow(row: NamingOverride): void {
    if (!row?.id) return;
    const desc = `${row.entityType} · ${row.scopeLevel}${row.subtypeCode ? ' · ' + row.subtypeCode : ''}`;
    if (typeof window !== 'undefined' &&
        !window.confirm(`Delete override for ${desc}?`)) return;
    this.engine.deleteNamingOverride(row.id, environment.defaultTenantId).subscribe({
      next: () => { this.status = 'Override deleted.'; this.reload(); },
      error: (err) => {
        this.status = err?.status === 403
          ? 'Forbidden — lacks delete:NamingTemplateOverride.'
          : `Delete failed: ${err?.error?.detail ?? err?.message ?? err}`;
      },
    });
  }
}
