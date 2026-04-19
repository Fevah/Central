import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import {
  NetworkingEngineService, PortListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Tenant-wide port grid — sibling to the per-device Ports tab on
/// the device detail page (141f2ad96). Same thin list
/// (/api/net/ports) consumed without the deviceId narrower, so
/// operators can scan port density / utilization across every
/// device in one view.
///
/// Grouping by deviceHostname by default clusters each device's
/// ports together; interfacePrefix is available as a secondary
/// group via the grid's grouping panel for "show me every xe-
/// port in the tenant" scans.
///
/// Routed at /network/ports. Grid double-click drills to the
/// owning device's detail page (Ports tab highlighted by default
/// via the tab index field when the device-detail component
/// gets query-param support; for this slice the tab stays at 0
/// and the operator clicks through).
@Component({
  selector: 'app-network-ports',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Ports</h2>
      <small class="subtitle">net.port rows across every device in the tenant. Capped at 5000; drill into a device to see just its ports.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="ports" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true"
                   [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }"
                   [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="deviceHostname" caption="Device" [fixed]="true" width="180"
                  [groupIndex]="0" />
      <dxi-column dataField="interfaceName"   caption="Interface"  width="160" sortOrder="asc" [sortIndex]="0" />
      <dxi-column dataField="interfacePrefix" caption="Prefix"     width="80" />
      <dxi-column dataField="speedMbps"       caption="Speed (Mb)" width="100" dataType="number" />
      <dxi-column dataField="adminUp"         caption="Admin up"   width="90"  dataType="boolean" />
      <dxi-column dataField="portMode"        caption="Mode"       width="100" />
      <dxi-column dataField="nativeVlanId"    caption="Native VLAN" width="110" dataType="number" />
      <dxi-column dataField="status"          caption="Status"     width="90" />
      <dxi-column dataField="description"     caption="Description" />
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
export class NetworkPortsComponent implements OnInit {
  ports: PortListRow[] = [];
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
    this.engine.listPorts(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.ports = rows;
        this.loading = false;
        this.status = `${rows.length} port${rows.length === 1 ? '' : 's'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — drill into a device for that device only)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.ports = [];
      },
    });
  }

  /// Double-click → port detail (Summary / Audit / Usage tabs).
  /// Device drill is reachable via the Summary tab's device link.
  onRowDoubleClick(e: { data: PortListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/port', row.id]);
  }
}
