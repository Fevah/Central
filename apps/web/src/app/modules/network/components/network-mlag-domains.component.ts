import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, MlagDomainListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide MLAG domain grid. Sibling to the other thin-list
/// grids (ports, ae, ip-addresses). Grouped by poolCode by default
/// so operators see pool cardinality at a glance.
@Component({
  selector: 'app-network-mlag-domains',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>MLAG domains</h2>
      <small class="subtitle">net.mlag_domain rows across the tenant. Domain ids are carved from MLAG pools; scope_level pins each domain to a Region / Site / Building.</small>
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
                   [groupPanel]="{ visible: true }">
      <dxi-column dataField="poolCode"    caption="Pool"        width="160" [groupIndex]="0" />
      <dxi-column dataField="domainId"    caption="Domain id"   width="110" dataType="number"
                  sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="displayName" caption="Display name" width="260" />
      <dxi-column dataField="scopeLevel"  caption="Scope"       width="110" />
      <dxi-column dataField="status"      caption="Status"      width="90" />
      <dxi-column dataField="version"     caption="v"           width="50"  dataType="number" />
      <dxi-column dataField="id"          caption="UUID" />
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
export class NetworkMlagDomainsComponent implements OnInit {
  rows: MlagDomainListRow[] = [];
  loading = false;
  status = '';

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void { this.reload(); }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listMlagDomains(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rows = rows;
        this.loading = false;
        this.status = `${rows.length} domain${rows.length === 1 ? '' : 's'}`;
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.rows = [];
      },
    });
  }
}
