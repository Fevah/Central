import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, ResolvedRule,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Full validation-rule catalog browser. Read-only grid over
/// /api/net/validation/rules — lets operators see every rule
/// code + what it does + the tenant-effective severity + enabled
/// state. Complement to the violation list on /network/validation
/// which only shows rules that fired on the current data.
///
/// Writes (toggling enabled, overriding severity) stay WPF-only
/// until the web approval chrome catches up.
@Component({
  selector: 'app-network-validation-rules',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Validation rule catalog</h2>
      <small class="subtitle">
        Every rule the engine ships with, plus this tenant's
        effective severity + enabled state after any overrides.
        Read-only here; the WPF client exposes the edit surface.
      </small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <dx-button text="Run validation →" type="default" icon="check"
                 routerLink="/network/validation" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rules" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="category"          caption="Category"    [groupIndex]="0" width="130" />
      <dxi-column dataField="code"              caption="Code"        [fixed]="true" width="320"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="name"              caption="Name" />
      <dxi-column dataField="effectiveSeverity" caption="Severity"    width="100"
                  cellTemplate="sevTemplate" />
      <dxi-column dataField="effectiveEnabled"  caption="Enabled"     width="90"
                  dataType="boolean" />
      <dxi-column dataField="hasTenantOverride" caption="Override"    width="100"
                  dataType="boolean" cellTemplate="overrideTemplate" />
      <dxi-column dataField="defaultSeverity"   caption="Default sev" width="110" />
      <dxi-column dataField="description"       caption="Description" />

      <div *dxTemplate="let d of 'sevTemplate'">
        <span [class]="'sev-' + (d.value || '').toLowerCase()">● {{ d.value }}</span>
      </div>
      <div *dxTemplate="let d of 'overrideTemplate'">
        <span *ngIf="d.value" class="override-badge">✎</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    :host { display: block; padding: 12px 16px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin: 12px 0; }
    .status-line { color: #666; font-size: 12px; margin-left: auto; }
    .sev-error   { color: #ef4444; }
    .sev-warning { color: #f59e0b; }
    .sev-info    { color: #3b82f6; }
    .override-badge {
      display: inline-block; padding: 1px 6px; border-radius: 4px;
      background: rgba(234,179,8,0.2); color: #eab308;
      font-size: 11px; font-weight: 600;
    }
  `],
})
export class NetworkValidationRulesComponent implements OnInit {
  rules: ResolvedRule[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void { this.reload(); }

  /// Double-click a rule row → run just that rule on
  /// /network/validation via the ruleCode query param.
  onRowDoubleClick(e: { data: ResolvedRule }): void {
    const code = e?.data?.code;
    if (!code) return;
    this.router.navigate(['/network/validation'], {
      queryParams: { ruleCode: code },
    });
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listValidationRules(environment.defaultTenantId).subscribe({
      next: (rs) => {
        this.rules = rs ?? [];
        this.loading = false;
        this.status = `${this.rules.length} rule${this.rules.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rules = [];
      },
    });
  }
}
