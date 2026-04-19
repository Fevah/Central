import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, ValidationRunResult, Violation,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Data-quality dashboard — runs every enabled validation rule
/// + presents the violations grouped for operator scanning.
///
/// Complements the /network/validation page (per-rule detail +
/// fix-it flow) with a hero view for "which entities have the
/// most problems?" scans. Summary cards at the top show rules
/// run / rules with findings / total violations, matching the
/// engine's ValidationRunResult counters.
///
/// Rows pre-decorated with a `severityRank` numeric so sorting
/// groups Error/Warning/Info cleanly (DxDataGrid's default string
/// sort puts Warning before Error).
@Component({
  selector: 'app-network-data-quality',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Data quality</h2>
      <small class="subtitle">Full validation-rule run. Grouped by entity type + rule code so "which entities need the most attention?" is a glance.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Run validation" icon="check" type="default"
                 stylingMode="contained" (onClick)="run()" [disabled]="running" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <!-- Summary cards -->
    <div *ngIf="result" class="cards">
      <div class="card card-total">
        <div class="card-value">{{ result.rulesRun }}</div>
        <div class="card-label">Rules run</div>
      </div>
      <div class="card card-triggered">
        <div class="card-value">{{ result.rulesWithFindings }}</div>
        <div class="card-label">Rules with findings</div>
      </div>
      <div class="card card-violations">
        <div class="card-value">{{ result.totalViolations }}</div>
        <div class="card-label">Total violations</div>
      </div>
    </div>

    <!-- Violations grid -->
    <dx-data-grid *ngIf="result"
                   [dataSource]="decorated" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="entityType"   caption="Entity type" width="180" [groupIndex]="0" />
      <dxi-column dataField="severity"     caption="Severity"    width="100" [groupIndex]="1"
                  cellTemplate="severityTemplate"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="ruleCode"     caption="Rule"        width="320" />
      <dxi-column dataField="message"      caption="Message" />
      <dxi-column dataField="entityId"     caption="Entity id"   width="260" />
      <dxi-column dataField="severityRank" caption="sort"        width="1"   [visible]="false" />

      <div *dxTemplate="let d of 'severityTemplate'">
        <span [class]="'badge badge-' + d.value.toLowerCase()">{{ d.value }}</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 12px; }
    .status-line { color: #666; font-size: 12px; }
    .cards { display: flex; gap: 16px; margin-bottom: 16px; }
    .card { flex: 1; padding: 16px; border-radius: 8px; text-align: center; }
    .card-total      { background: rgba(59,130,246,0.1); border: 1px solid rgba(59,130,246,0.3); }
    .card-triggered  { background: rgba(234,179,8,0.1);   border: 1px solid rgba(234,179,8,0.3); }
    .card-violations { background: rgba(239,68,68,0.1);   border: 1px solid rgba(239,68,68,0.3); }
    .card-value { font-size: 28px; font-weight: bold; }
    .card-total .card-value      { color: #60a5fa; }
    .card-triggered .card-value  { color: #eab308; }
    .card-violations .card-value { color: #ef4444; }
    .card-label { font-size: 11px; color: #9ca3af; text-transform: uppercase; letter-spacing: 0.5px; }
    .badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .badge-error   { background: rgba(239,68,68,0.2); color: #ef4444; }
    .badge-warning { background: rgba(234,179,8,0.2); color: #eab308; }
    .badge-info    { background: rgba(59,130,246,0.2); color: #60a5fa; }
  `]
})
export class NetworkDataQualityComponent implements OnInit {
  result: ValidationRunResult | null = null;
  decorated: (Violation & { severityRank: number })[] = [];
  running = false;
  status = '';

  /// Routes to drill from a violation row to the matching detail
  /// page. Entity types not in this map fall back to audit.
  private readonly detailRouteByType: Record<string, string> = {
    Device:            '/network/net-device',
    Server:            '/network/net-server',
    Vlan:              '/network/net-vlan',
    Link:              '/network/net-link',
    Subnet:            '/network/net-subnet',
    DhcpRelayTarget:   '/network/net-dhcp-relay',
    IpAddress:         '/network/ip-address',
    Region:            '/network/region',
    Site:              '/network/site',
    Building:          '/network/building',
    Floor:             '/network/floor',
  };

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void { this.run(); }

  run(): void {
    this.running = true;
    this.status = 'Running validation…';
    this.engine.runValidation(environment.defaultTenantId).subscribe({
      next: (r) => {
        this.result = r;
        this.decorated = r.violations.map(v => ({
          ...v,
          severityRank: v.severity === 'Error' ? 0 : v.severity === 'Warning' ? 1 : 2,
        }));
        this.running = false;
        this.status = r.totalViolations === 0
          ? 'No violations found. ✨'
          : `${r.totalViolations} violation${r.totalViolations === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.running = false;
        this.status = `Run failed: ${err?.error?.detail ?? err?.message ?? err}`;
        this.result = null;
      },
    });
  }

  /// Double-click a violation row → detail page for the offending
  /// entity. Falls back to audit when the entity type isn't on
  /// the web detail-page map (+ when entityId is null, which
  /// happens for catalog-level rules like device_role.*).
  onRowDoubleClick(e: { data: Violation }): void {
    const v = e?.data;
    if (!v?.entityId) return;
    const route = this.detailRouteByType[v.entityType];
    if (route) {
      this.router.navigate([route, v.entityId]);
      return;
    }
    this.router.navigate(['/network/audit', v.entityType, v.entityId]);
  }
}
