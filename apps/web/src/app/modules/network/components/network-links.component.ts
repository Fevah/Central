import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DxDataGridModule, DxButtonModule } from 'devextreme-angular';
import { NetworkingEngineService, LinkListRow } from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Web counterpart to the WPF LinkGridPanel (P2P / B2B / FW collapsed
/// into one grid — the grouping UX happens via the linkType column's
/// header filter, not separate pages). Backed by /api/net/links.
///
/// A separate /network/links page already exists for BGP-peering
/// details; this one (/network/links-grid) is the thin multi-tenant
/// view that matches the other net.* grids.
@Component({
  selector: 'app-network-links-grid',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxButtonModule],
  template: `
    <div class="page-header">
      <h2>Links (net.*)</h2>
      <small class="subtitle">Unified net.link rows — P2P, B2B, FW, DMZ, MLAG-Peer, Server-NIC, WAN. Filter by linkType to narrow.</small>
    </div>

    <div class="toolbar">
      <dx-button text="Refresh" icon="refresh" stylingMode="text"
                 (onClick)="reload()" [disabled]="loading" />
      <span *ngIf="status" class="status-line">{{ status }}</span>
    </div>

    <dx-data-grid [dataSource]="links" [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true" [searchPanel]="{ visible: true }"
                   [filterRow]="{ visible: true }" [headerFilter]="{ visible: true }"
                   [groupPanel]="{ visible: true }"
                   (onRowDblClick)="onRowDoubleClick($event)">
      <dxi-column dataField="linkCode" caption="Link code" [fixed]="true" width="180" sortOrder="asc" />
      <dxi-column dataField="linkType" caption="Type"      width="120" [groupIndex]="0" />
      <dxi-column dataField="deviceA"  caption="Device A"  width="180" />
      <dxi-column dataField="deviceB"  caption="Device B"  width="180" />
      <dxi-column dataField="status"   caption="Status"    width="90" />
      <dxi-column dataField="version"  caption="v"         width="50"  dataType="number" />
      <dxi-column dataField="id"       caption="UUID"      width="260" />
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
export class NetworkLinksGridComponent implements OnInit {
  links: LinkListRow[] = [];
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
    this.engine.listLinks(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.links = rows;
        this.loading = false;
        this.status = `${rows.length} link${rows.length === 1 ? '' : 's'}` +
          (rows.length >= 5000 ? ' (capped at 5000 — use Search for narrowing)' : '');
      },
      error: (err) => {
        this.loading = false;
        this.status = `Load failed: ${err?.message ?? err}`;
        this.links = [];
      },
    });
  }

  onRowDoubleClick(e: { data: LinkListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/audit', 'Link', row.id]);
  }
}
