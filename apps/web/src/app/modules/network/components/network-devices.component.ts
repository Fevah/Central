import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, DeviceListRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF DeviceGridPanel / IPAM grid. Reads
/// from the engine's thin list endpoint (`/api/net/devices`, capped
/// at 5000 rows per tenant) — distinct from the legacy
/// network-dashboard grid which queries Central.Api's switch_guide.
///
/// The two grids will converge eventually (dual-write trigger keeps
/// hostname + status in sync between public.switches and net.device)
/// but for this slice they coexist: operators can browse EITHER the
/// legacy switch_guide-backed dashboard OR the net.device-backed
/// list, with the same grid/filter/search UX.
@Component({
  selector: 'app-network-devices',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Devices</h2>
      <small class="subtitle">net.device rows — multi-tenant source-of-truth. Dual-writes keep the legacy switch_guide view in sync.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="devices" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="hostname"     caption="Hostname"     [fixed]="true" width="180" />
      <dxi-column dataField="roleCode"     caption="Role"         width="100" />
      <dxi-column dataField="buildingCode" caption="Building"     width="120" />
      <dxi-column dataField="status"       caption="Status"       width="90" />
      <dxi-column dataField="version"      caption="v"            width="50" />
      <dxi-column dataField="id"           caption="UUID"         width="260" />
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
export class NetworkDevicesComponent implements OnInit {
  devices: DeviceListRow[] = [];
  loading = false;
  status = '';

  constructor(
    private engine: NetworkingEngineService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading = true;
    this.status = 'Loading…';
    this.engine.listDevices(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.devices = rows;
        this.loading = false;
        this.status = `${rows.length} device${rows.length === 1 ? '' : 's'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — use Search for narrowing)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.devices = [];
      },
    });
  }

  /// Double-click → net.device detail page (tabbed Summary + Audit +
  /// Renders). Distinct from the legacy /network/devices/:id route
  /// which loads the switch_guide editor — this lands on /network/net-device
  /// for the net.* authoritative surface.
  onRowDoubleClick(e: { data: DeviceListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/net-device', row.id]);
  }
}
