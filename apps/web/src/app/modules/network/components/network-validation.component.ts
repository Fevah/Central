import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule, DxLoadIndicatorModule } from 'devextreme-angular';
import {
  NetworkingEngineService,
  ValidationRunResult,
  Violation,
  ResolvedRule,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

type SeverityFilter = 'All' | 'Error' | 'Warning' | 'Info';
type CategoryFilter = 'All' | 'Integrity' | 'Consistency' | 'Safety' | 'Advisory';

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
      <dx-button *ngFor="let c of runableCategories"
                 [text]="'Run ' + c"
                 stylingMode="outlined"
                 (onClick)="run(c)" [disabled]="busy" />
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

    <div *ngIf="violations.length > 0 && categoryMap.size > 0" class="filter-bar">
      <span class="filter-label">Category:</span>
      <dx-button *ngFor="let f of categoryFilters"
                 [text]="f + ' (' + categoryCount(f) + ')'"
                 [type]="category === f ? 'default' : 'normal'"
                 stylingMode="outlined"
                 (onClick)="setCategory(f)" />
      <dx-button *ngIf="severity !== 'All' || category !== 'All'"
                 text="Reset filters" stylingMode="text"
                 icon="clear"
                 (onClick)="resetFilters()" />
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
export class NetworkValidationComponent implements OnInit {
  violations: Violation[] = [];
  filteredViolations: Violation[] = [];
  summary = '';
  status = '';
  busy = false;

  severityFilters: SeverityFilter[] = ['All', 'Error', 'Warning', 'Info'];
  severity: SeverityFilter = 'All';

  categoryFilters: CategoryFilter[] = ['All', 'Integrity', 'Consistency', 'Safety', 'Advisory'];
  category: CategoryFilter = 'All';

  /// Categories offered as "Run <category>" toolbar buttons —
  /// drops the All sentinel (the existing Run button covers it)
  /// so the four buckets get their own quick-run.
  readonly runableCategories = ['Integrity', 'Consistency', 'Safety', 'Advisory'];

  /// ruleCode → category map built from /api/net/validation/rules.
  /// Used to derive a Violation's category at filter time since
  /// the Violation shape doesn't carry it.
  categoryMap = new Map<string, string>();

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
    private route: ActivatedRoute,
  ) {}

  ngOnInit(): void {
    // Load the rule catalog once so category filtering works from
    // the first violation the operator sees. Silent-fail: if the
    // catalog call errors, the Category filter bar just doesn't
    // render (categoryMap stays empty).
    this.engine.listValidationRules(environment.defaultTenantId).subscribe({
      next: (rules) => {
        for (const r of rules ?? []) {
          this.categoryMap.set(r.code, r.category);
        }
      },
      error: () => { /* silent */ },
    });

    // Optional deep-link: /network/validation?ruleCode=foo.bar runs
    // just that rule on landing. Drives the rules-catalog page's
    // double-click drill + the "run one rule" WPF handoff flow.
    const qp = this.route.snapshot.queryParamMap;
    const ruleCode = qp.get('ruleCode');
    if (ruleCode) {
      this.runSingle(ruleCode);
    }
  }

  /// Run one specific rule by code. Used by the ruleCode query
  /// param drill; sets the summary line explicitly so operators
  /// see which rule ran rather than a generic message.
  private runSingle(ruleCode: string): void {
    this.busy = true;
    this.status = `Running rule ${ruleCode}…`;
    this.summary = '';
    this.engine.runValidation(environment.defaultTenantId, ruleCode).subscribe({
      next: (result: ValidationRunResult) => {
        this.violations = result.violations;
        this.applyFilter();
        this.summary = `${result.rulesRun} rule run · ` +
                       `${result.totalViolations} violation${result.totalViolations === 1 ? '' : 's'}`;
        this.status = result.totalViolations === 0
          ? `${ruleCode} — clean.`
          : '';
        this.busy = false;
      },
      error: (err) => {
        this.busy = false;
        this.status = `Validation failed: ${err?.message ?? err}`;
      },
    });
  }

  /// Runs the full catalog when no category is supplied, or just
  /// the requested category. The category filter is server-side
  /// (see RunValidationBody.category) so the rules_run counter
  /// reflects only the executed subset.
  run(category?: string): void {
    this.busy = true;
    this.status = category
      ? `Running ${category} rules…`
      : 'Running validation…';
    this.summary = '';
    // When running a category, auto-activate the matching client-
    // side filter so the grid shows only those rows instead of
    // stale violations from prior broader runs.
    if (category) {
      const match = this.categoryFilters.find(f => f === category);
      if (match) this.category = match;
    }
    this.engine.runValidation(environment.defaultTenantId, undefined, category).subscribe({
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

  setCategory(f: CategoryFilter): void {
    this.category = f;
    this.applyFilter();
  }

  /// Reset both severity + category to All in one click. Appears
  /// only when either filter is active.
  resetFilters(): void {
    this.severity = 'All';
    this.category = 'All';
    this.applyFilter();
  }

  /// Count per severity — drives the "(N)" badges on the filter
  /// buttons so operators see the breakdown before clicking.
  severityCount(f: SeverityFilter): number {
    if (f === 'All') return this.violations.length;
    return this.violations.filter(v => v.severity === f).length;
  }

  /// Count per category — derived via categoryMap (ruleCode →
  /// category). Falls back to 0 when the map hasn't loaded yet.
  categoryCount(f: CategoryFilter): number {
    if (f === 'All') return this.violations.length;
    return this.violations.filter(v => this.categoryMap.get(v.ruleCode) === f).length;
  }

  private applyFilter(): void {
    let rows = this.violations;
    if (this.severity !== 'All') {
      rows = rows.filter(v => v.severity === this.severity);
    }
    if (this.category !== 'All') {
      rows = rows.filter(v => this.categoryMap.get(v.ruleCode) === this.category);
    }
    this.filteredViolations = rows;
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
