import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DxTextAreaModule, DxButtonModule, DxSelectBoxModule, DxDataGridModule } from 'devextreme-angular';
import {
  NetworkingEngineService, ImportValidationResult, ImportRowOutcome,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF BulkPanel, validate-only. Operators
/// paste a CSV, pick entity + mode, hit Validate, see the per-row
/// outcomes. Apply is WPF-only today because the web doesn't yet
/// have a write-confirm dialog that distinguishes dry-run from
/// real-apply strongly enough to prevent accidental commits.
///
/// Headers are the canonical ones the engine enforces — same
/// mapping the WPF "Copy header" button uses so pasting here +
/// editing lands a parseable document.
@Component({
  selector: 'app-network-bulk',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxTextAreaModule, DxButtonModule, DxSelectBoxModule, DxDataGridModule],
  template: `
    <div class="page-header">
      <h2>Bulk import — validate</h2>
      <small class="subtitle">Paste a CSV, pick entity + mode, Validate. Apply (real DB writes) stays on the WPF client for now.</small>
    </div>

    <div class="toolbar">
      <label>Entity</label>
      <dx-select-box class="sm" [items]="entities" displayExpr="label" valueExpr="key"
                     [(value)]="selectedEntity" (onValueChanged)="onEntityChanged()" />

      <label>Mode</label>
      <dx-select-box class="xs" [items]="modes" [(value)]="selectedMode" />

      <dx-button text="Copy header" stylingMode="outlined"
                 hint="Copy the canonical CSV header for the selected entity to clipboard"
                 (onClick)="copyHeader()" />
      <dx-button text="Validate" type="default" icon="check"
                 (onClick)="validate()" [disabled]="busy" />
      <dx-button text="Clear" (onClick)="clear()" />
    </div>

    <dx-text-area [(value)]="csvBody" [height]="260"
                  placeholder="Paste CSV here — first line is the header (use Copy header for the canonical shape)…"
                  class="csv-editor" />

    <div *ngIf="summary" class="summary" [class.ok]="result?.invalid === 0"
                                            [class.err]="(result?.invalid ?? 0) > 0">
      <strong>{{ verb }}</strong> · {{ summary }}
    </div>

    <dx-data-grid *ngIf="outcomes.length > 0"
                  [dataSource]="outcomes" [showBorders]="true" [hoverStateEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  [headerFilter]="{ visible: true }" [searchPanel]="{ visible: true }">
      <dxi-column dataField="rowNumber" caption="Row"    width="60" />
      <dxi-column dataField="ok"        caption="OK"     width="50"
                  cellTemplate="okTemplate" />
      <dxi-column dataField="identifier" caption="Identifier" width="200" />
      <dxi-column dataField="errorText"  caption="Errors" />

      <div *dxTemplate="let d of 'okTemplate'">
        <span [class]="d.value ? 'status-ok' : 'status-err'">● {{ d.value ? 'Y' : 'N' }}</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 10px; align-items: center; flex-wrap: wrap; margin-bottom: 10px; }
    .toolbar label { color: #888; font-size: 12px; margin-right: -4px; }
    .toolbar .xs { width: 110px; }
    .toolbar .sm { width: 220px; }
    .csv-editor { margin-bottom: 10px; font-family: Consolas, monospace; }
    .summary { padding: 8px 12px; margin-bottom: 10px; border-left: 3px solid #666; background: #1f2937; }
    .summary.ok  { border-left-color: #22c55e; }
    .summary.err { border-left-color: #ef4444; }
    .status-ok  { color: #22c55e; }
    .status-err { color: #ef4444; }
  `]
})
export class NetworkBulkComponent {
  /// Entity options — label is for display, key routes to the
  /// engine URL segment (`/api/net/{key}/import`). Order matches
  /// WPF's EntityCombo for operator muscle-memory parity.
  entities = [
    { key: 'devices',            label: 'Devices' },
    { key: 'vlans',              label: 'VLANs' },
    { key: 'subnets',            label: 'Subnets' },
    { key: 'servers',            label: 'Servers' },
    { key: 'links',              label: 'Links' },
    { key: 'dhcp-relay-targets', label: 'DHCP relay targets' },
  ];
  modes = ['create', 'upsert'];

  /// Canonical CSV headers — kept in lockstep with the engine's
  /// `*_COLUMNS` constants AND the WPF BulkPanel's
  /// `CanonicalHeaders` dict. Edits land in all three places.
  private readonly headers: Record<string, string> = {
    'devices':            'hostname,role_code,building_code,site_code,management_ip,asn,status,version',
    'vlans':              'vlan_id,display_name,description,scope_level,scope_entity_code,template_code,block_code,status',
    'subnets':            'subnet_code,display_name,network,vlan_id,pool_code,scope_level,scope_entity_code,status',
    'servers':            'hostname,profile_code,building_code,asn,loopback_ip,management_ip,nic_count,status',
    'links':              'link_code,link_type,vlan_id,subnet_code,device_a,port_a,ip_a,device_b,port_b,ip_b,status',
    'dhcp-relay-targets': 'vlan_id,server_ip,priority,linked_ip_address_id,notes,status',
  };

  selectedEntity: string = 'devices';
  selectedMode: 'create' | 'upsert' = 'create';
  csvBody = '';
  busy = false;

  result: ImportValidationResult | null = null;
  outcomes: (ImportRowOutcome & { errorText: string })[] = [];
  summary = '';
  verb = '';

  constructor(private engine: NetworkingEngineService) {}

  onEntityChanged(): void {
    // Intentionally empty — header is copy-on-demand, not auto-
    // stuffed into the editor (operators often paste their whole
    // document including their own header).
  }

  copyHeader(): void {
    const h = this.headers[this.selectedEntity];
    if (!h) return;
    navigator.clipboard.writeText(h).catch(() => {
      // Clipboard write can fail on http:// / insecure context;
      // fall back to stuffing the header into the editor so
      // operators still get it.
      this.csvBody = h + '\r\n' + this.csvBody;
    });
  }

  validate(): void {
    if (!this.csvBody || this.csvBody.trim().length === 0) {
      this.summary = 'Editor is empty — paste a CSV first.';
      this.verb = 'SKIPPED';
      this.outcomes = [];
      return;
    }
    this.busy = true;
    this.summary = 'Validating…';
    this.verb = 'RUNNING';
    this.engine
      .validateBulk(this.selectedEntity as any, environment.defaultTenantId,
                    this.csvBody, this.selectedMode)
      .subscribe({
        next: (r) => {
          this.result = r;
          this.outcomes = r.outcomes.map(o => ({
            ...o,
            errorText: o.errors?.length ? o.errors.join('; ') : '',
          }));
          this.verb = r.dryRun ? 'DRY-RUN' : (r.applied ? 'APPLIED' : 'NOT APPLIED');
          this.summary = `${r.totalRows} rows · ${r.valid} valid · ${r.invalid} invalid`;
          this.busy = false;
        },
        error: (err) => {
          this.verb = 'ERROR';
          this.summary = `Validation failed: ${err?.message ?? err}`;
          this.outcomes = [];
          this.result = null;
          this.busy = false;
        },
      });
  }

  clear(): void {
    this.csvBody = '';
    this.result = null;
    this.outcomes = [];
    this.summary = '';
    this.verb = '';
  }
}
