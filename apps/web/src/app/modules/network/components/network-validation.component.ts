import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule, DxLoadIndicatorModule } from 'devextreme-angular';
import {
  NetworkingEngineService,
  ValidationRunResult,
  Violation,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

type SeverityFilter = 'All' | 'Error' | 'Warning' | 'Info';

/// Web counterpart to the WPF Validation panel (Phase 9a) — runs
/// the Rust-owned rule catalog against the tenant's net.* data
/// and renders per-violation rows. No rule editing yet (WPF can
/// toggle severity / enabled per tenant; web is read-only for
/// this slice). Clicking a violation with an entity_id drills to
/// the audit timeline for that entity — quick "who changed this
/// and did that cause the violation?" workflow.
@Component({
  selector: 'app-network-validation',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule, DxLoadIndicatorModule],
  template: `
    <div class="page-header">
      <h2>Validation</h2>
      <small class="subtitle">Run the rule engine across the tenant's net.* data. Rules live in Rust (services/networking-engine/src/validation.rs).</small>
    </div>

    <div class="toolbar">
      <dx-button text="Run all rules" type="default" icon="check"
                 (onClick)="run()" [disabled]="busy" />
      <dx-load-indicator *ngIf="busy" height="24" width="24" />
      <span *ngIf="summary" class="summary">{{ summary }}</span>
    </div>

    <div *ngIf="violations.length > 0" class="filter-bar">
      <span class="filter-label">Severity:</span>
      <dx-button *ngFor="let f of severityFilters"
                 [text]="f + ' (' + severityCount(f) + ')'"
                 [type]="severity === f ? 'default' : 'normal'"
                 stylingMode="outlined"
                 (onClick)="setSeverity(f)" />
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-data-grid [dataSource]="filteredViolations" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="severity"    caption="Severity"   [groupIndex]="0" width="100"
                  cellTemplate="severityTemplate" />
      <dxi-column dataField="ruleCode"    caption="Rule"       width="280" />
      <dxi-column dataField="entityType"  caption="Entity type" width="130" />
      <dxi-column dataField="entityId"    caption="Entity id"  width="260" />
      <dxi-column dataField="message"     caption="Message" />

      <div *dxTemplate="let d of 'severityTemplate'">
        <span [class]="'sev-' + (d.value || '').toLowerCase()">● {{ d.value }}</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .summary { color: #666; font-size: 12px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .filter-bar { display: flex; gap: 8px; align-items: center; margin: 10px 0; flex-wrap: wrap; }
    .filter-label { color: #57606a; font-size: 12px; font-weight: 500; }
    .sev-error   { color: #ef4444; }
    .sev-warning { color: #f59e0b; }
    .sev-info    { color: #3b82f6; }
  `]
})
export class NetworkValidationComponent {
  violations: Violation[] = [];
  filteredViolations: Violation[] = [];
  summary = '';
  status = '';
  busy = false;

  severityFilters: SeverityFilter[] = ['All', 'Error', 'Warning', 'Info'];
  severity: SeverityFilter = 'All';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  run(): void {
    this.busy = true;
    this.status = 'Running validation…';
    this.summary = '';
    this.engine.runValidation(environment.defaultTenantId).subscribe({
      next: (result: ValidationRunResult) => {
        this.violations = result.violations;
        this.applyFilter();
        this.summary = `${result.rulesRun} rule${result.rulesRun === 1 ? '' : 's'} run · ` +
                       `${result.rulesWithFindings} with findings · ` +
                       `${result.totalViolations} violation${result.totalViolations === 1 ? '' : 's'}`;
        this.status = result.totalViolations === 0
          ? 'Clean run — no violations.'
          : '';
        this.busy = false;
      },
      error: (err) => {
        this.busy = false;
        this.status = `Validation failed: ${err?.message ?? err}`;
      },
    });
  }

  setSeverity(f: SeverityFilter): void {
    this.severity = f;
    this.applyFilter();
  }

  /// Count per severity — drives the "(N)" badges on the filter
  /// buttons so operators see the breakdown before clicking.
  severityCount(f: SeverityFilter): number {
    if (f === 'All') return this.violations.length;
    return this.violations.filter(v => v.severity === f).length;
  }

  private applyFilter(): void {
    this.filteredViolations = this.severity === 'All'
      ? [...this.violations]
      : this.violations.filter(v => v.severity === this.severity);
  }

  /// Drill to the audit timeline for the offending entity. The
  /// /api/net/audit/entity endpoint accepts any entity_type string
  /// so this works even for rows where the validation entity type
  /// doesn't have a dedicated detail page yet.
  onRowDoubleClick(e: { data: Violation }): void {
    const v = e?.data;
    if (!v || !v.entityId) return;
    this.router.navigate(['/network/audit', v.entityType, v.entityId]);
  }
}
