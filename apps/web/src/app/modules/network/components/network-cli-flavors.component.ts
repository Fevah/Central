import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSwitchModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ResolvedCliFlavor,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// CLI flavor admin page — enable/disable each renderer per tenant
/// + pick one default. Every catalog entry is always shown; rows
/// without a tenant override fall back to the catalog default.
///
/// Writes go through PUT /api/net/cli-flavors/:code, which also
/// enforces one-default-per-tenant by clearing the flag on any
/// other row.
@Component({
  selector: 'app-network-cli-flavors',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSwitchModule],
  template: `
    <div class="page-header">
      <h2>CLI flavors</h2>
      <small class="subtitle">Per-tenant config for which renderer powers config-gen. Only PicOS is GA today; the others are metadata stubs + render to 'not implemented'.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true">
      <dxi-column dataField="code"             caption="Code"        [fixed]="true" width="140" sortOrder="asc" />
      <dxi-column dataField="displayName"      caption="Name"        width="220" />
      <dxi-column dataField="vendor"           caption="Vendor"      width="100" />
      <dxi-column dataField="status"           caption="Status"      width="90"  cellTemplate="statusTemplate" />
      <dxi-column dataField="effectiveEnabled" caption="Enabled"     width="100" cellTemplate="enabledTemplate" />
      <dxi-column dataField="isDefault"        caption="Default"     width="100" cellTemplate="defaultTemplate" />
      <dxi-column dataField="description"      caption="Description" />
      <dxi-column dataField="updatedAt"        caption="Updated"     width="170" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />

      <div *dxTemplate="let d of 'statusTemplate'">
        <span [class]="'badge badge-' + (d.value || '').toLowerCase()">{{ d.value }}</span>
      </div>
      <div *dxTemplate="let d of 'enabledTemplate'">
        <dx-switch [(value)]="d.data.effectiveEnabled"
                   (onValueChanged)="saveEnabled(d.data, $event)"
                   [disabled]="saving" />
      </div>
      <div *dxTemplate="let d of 'defaultTemplate'">
        <dx-switch [(value)]="d.data.isDefault"
                   (onValueChanged)="saveDefault(d.data, $event)"
                   [disabled]="saving || !d.data.effectiveEnabled" />
      </div>
    </dx-data-grid>

    <div class="footer-note">
      <strong>Only one flavor can be default per tenant.</strong>
      Flipping the default switch on a new row automatically clears the flag on any previous default.
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-ga   { background: rgba(34,197,94,0.2);  color: #22c55e; }
    .badge-beta { background: rgba(234,179,8,0.2); color: #eab308; }
    .badge-stub { background: rgba(107,114,128,0.2); color: #9ca3af; }
    .footer-note { margin-top: 12px; padding: 10px 14px; color: #94a3b8; font-size: 12px; background: rgba(59,130,246,0.08); border: 1px solid rgba(59,130,246,0.2); border-radius: 4px; }
    .footer-note strong { color: #60a5fa; }
  `]
})
export class NetworkCliFlavorsComponent implements OnInit {
  rows: ResolvedCliFlavor[] = [];
  loading = false;
  saving = false;
  status = '';

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listCliFlavors(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} flavor${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Flip the effective-enabled switch. `value` on the DxSwitch
  /// event is the new value the user just toggled to.
  saveEnabled(row: ResolvedCliFlavor, e: { value?: boolean; previousValue?: boolean }): void {
    if (e.value == null) return;
    if (e.value === e.previousValue) return;
    this.saveConfig(row, { enabled: e.value });
  }

  /// Flip the default switch. A disabled-effective row can't be
  /// default, so the template disables the switch in that case;
  /// this handler still guards server-side in case the UI state
  /// is stale.
  saveDefault(row: ResolvedCliFlavor, e: { value?: boolean; previousValue?: boolean }): void {
    if (e.value == null) return;
    if (e.value === e.previousValue) return;
    if (e.value && !row.effectiveEnabled) {
      this.status = `Can't make ${row.code} default while it's disabled.`;
      row.isDefault = false;
      return;
    }
    this.saveConfig(row, { isDefault: e.value });
  }

  private saveConfig(row: ResolvedCliFlavor, body: { enabled?: boolean; isDefault?: boolean }): void {
    this.saving = true;
    this.engine.setCliFlavorConfig(row.code, environment.defaultTenantId, body).subscribe({
      next: () => {
        // Reload so we pick up the one-default-per-tenant cascade
        // (toggling isDefault on row X clears it on row Y).
        this.saving = false;
        this.reload();
      },
      error: (err) => {
        this.saving = false;
        this.status = `Save failed: ${err?.error?.detail ?? err?.message ?? err}`;
        // Rollback the optimistic update since the reload won't fire.
        if (body.enabled !== undefined)   row.effectiveEnabled = !body.enabled;
        if (body.isDefault !== undefined) row.isDefault        = !body.isDefault;
      },
    });
  }
}
