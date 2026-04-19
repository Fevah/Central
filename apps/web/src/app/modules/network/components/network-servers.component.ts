import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, ServerListRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF ServerGridPanel. Backed by the engine's
/// thin /api/net/servers endpoint (5000-row cap, profileCode +
/// buildingCode resolved via LEFT JOIN). Double-click drills to the
/// audit timeline for the server uuid.
@Component({
  selector: 'app-network-servers',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Servers</h2>
      <small class="subtitle">net.server rows — hardware inventory alongside switches, with the same management-plane columns.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="servers" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="hostname"     caption="Hostname"     [fixed]="true" width="200" sortOrder="asc" />
      <dxi-column dataField="profileCode"  caption="Profile"      width="130" />
      <dxi-column dataField="buildingCode" caption="Building"     width="120" />
      <dxi-column dataField="status"       caption="Status"       width="90" />
      <dxi-column dataField="version"      caption="v"            width="50"  dataType="number" />
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
export class NetworkServersComponent implements OnInit {
  servers: ServerListRow[] = [];
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
    this.engine.listServers(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.servers = rows;
        this.loading = false;
        this.status = `${rows.length} server${rows.length === 1 ? '' : 's'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — use Search for narrowing)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.servers = [];
      },
    });
  }

  /// Double-click → server detail (tabbed Summary + Audit). Audit
  /// drill is reachable via the detail page's Audit tab rather than
  /// being the primary drill target — matches the device-grid UX.
  onRowDoubleClick(e: { data: ServerListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/net-server', row.id]);
  }
}
