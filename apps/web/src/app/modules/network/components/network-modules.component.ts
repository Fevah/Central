import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, ModuleListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide hardware module grid — linecards / transceivers /
/// PSUs / fans installed in every device. Grouped by deviceHostname
/// then moduleType so operators can scan "every transceiver in
/// this core" or "every PSU in the site" via the header filter.
@Component({
  selector: 'app-network-modules',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Modules</h2>
      <small class="subtitle">Physical hardware — linecards / transceivers / PSUs / fans across every device in the tenant.</small>
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
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="deviceHostname" caption="Device"  [fixed]="true" width="180"
                  [groupIndex]="0" />
      <dxi-column dataField="slot"           caption="Slot"    width="100" sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="moduleType"     caption="Type"    width="120" [groupIndex]="1" />
      <dxi-column dataField="model"          caption="Model"   width="160" />
      <dxi-column dataField="partNumber"     caption="Part no" width="140" />
      <dxi-column dataField="serialNumber"   caption="Serial"  width="180" />
      <dxi-column dataField="status"         caption="Status"  width="90" />
      <dxi-column dataField="version"        caption="v"       width="50"  dataType="number" />
      <dxi-column dataField="id"             caption="UUID" />
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .subtitle { color: #888; }
    .toolbar { display: flex; gap: 12px; align-items: center; margin-bottom: 8px; }
    .status-line { color: #666; font-size: 12px; }
  `]
})
export class NetworkModulesComponent implements OnInit {
  rows: ModuleListRow[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listModules(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} module${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }

  /// Double-click → module detail. Device drill reachable from
  /// the module detail page's device link.
  onRowDoubleClick(e: { data: ModuleListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/module', row.id]);
  }
}
