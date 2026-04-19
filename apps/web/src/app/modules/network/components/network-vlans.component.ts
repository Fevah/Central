import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, VlanListRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF VlanGridPanel. Backed by the engine's
/// thin /api/net/vlans endpoint (5000-row cap, blockCode resolved
/// via LEFT JOIN). Double-click drills to the audit timeline for the
/// VLAN uuid — same vocabulary the WPF grid uses.
@Component({
  selector: 'app-network-vlans',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>VLANs</h2>
      <small class="subtitle">net.vlan rows — multi-tenant VLAN catalog with scope-level resolution.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="vlans" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="vlanId"      caption="VLAN"        width="80"  dataType="number" [fixed]="true" sortOrder="asc" />
      <dxi-column dataField="displayName" caption="Name"        width="220" />
      <dxi-column dataField="blockCode"   caption="Block"       width="120" />
      <dxi-column dataField="scopeLevel"  caption="Scope"       width="110" />
      <dxi-column dataField="status"      caption="Status"      width="90" />
      <dxi-column dataField="version"     caption="v"           width="50"  dataType="number" />
      <dxi-column dataField="id"          caption="UUID"        width="260" />
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
export class NetworkVlansComponent implements OnInit {
  vlans: VlanListRow[] = [];
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
    this.engine.listVlans(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.vlans = rows;
        this.loading = false;
        this.status = `${rows.length} VLAN${rows.length === 1 ? '' : 's'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — use Search for narrowing)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.vlans = [];
      },
    });
  }

  onRowDoubleClick(e: { data: VlanListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/audit', 'Vlan', row.id]);
  }
}
