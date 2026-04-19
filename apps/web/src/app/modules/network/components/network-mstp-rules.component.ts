import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, MstpRuleListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide MSTP priority rule grid. Amber-tints rules with
/// stepCount === 0 — config-gen emits nothing for those, so
/// operators should know before a render run.
@Component({
  selector: 'app-network-mstp-rules',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>MSTP priority rules</h2>
      <small class="subtitle">Spanning-tree bridge-priority policy rules. Rows with zero steps highlight in amber — config-gen emits nothing for empty rules.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="rows" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [onCellPrepared]="onCellPrepared">
      <dxi-column dataField="ruleCode"       caption="Rule code"   [fixed]="true" width="180"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="displayName"    caption="Display name" width="260" />
      <dxi-column dataField="scopeLevel"     caption="Scope"        width="110" />
      <dxi-column dataField="scopeEntityId"  caption="Scope uuid"   width="260" />
      <dxi-column dataField="stepCount"      caption="Steps"        width="80"  dataType="number" />
      <dxi-column dataField="status"         caption="Status"       width="90" />
      <dxi-column dataField="version"        caption="v"            width="50"  dataType="number" />
      <dxi-column dataField="id"             caption="UUID" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
    ::ng-deep .empty-rule-row { background: rgba(234,179,8,0.08) !important; }
  `]
})
export class NetworkMstpRulesComponent implements OnInit {
  rows: MstpRuleListRow[] = [];
  loading = false;
  status = '';

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listMstpRules(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} rule${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Amber-tint rows where stepCount === 0 (empty rules emit
  /// nothing at config-gen). Parallels the AE grid's
  /// under-populated highlight (2ec803869).
  onCellPrepared = (e: { rowType?: string; data?: MstpRuleListRow; cellElement?: HTMLElement }): void => {
    if (e.rowType !== 'data' || !e.data || !e.cellElement) return;
    if (e.data.stepCount === 0) {
      e.cellElement.classList.add('empty-rule-row');
    }
  };
}
