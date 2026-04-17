import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DxDataGridModule, DxToolbarModule, DxTabPanelModule } from 'devextreme-angular';
import notify from 'devextreme/ui/notify';
import { NetworkService, BgpPeer, BgpNeighbor, BgpNetwork } from '../../../core/services/network.service';

/**
 * BGP page — master/detail layout.
 *
 * Top: list of BGP configs (one per switch).
 * Bottom: tabs for the selected peer's neighbors and advertised networks.
 *
 * Mirrors the WPF Routing module's BGP master-detail panel. Sync-from-SSH
 * and add-neighbor actions are deferred to a later phase — read-only is
 * the most-asked-for view in the desktop usage patterns.
 */
@Component({
  selector: 'app-bgp-peers',
  standalone: true,
  imports: [CommonModule, RouterModule, DxDataGridModule, DxToolbarModule, DxTabPanelModule],
  template: `
    <dx-toolbar class="page-toolbar">
      <dxi-item location="before"><div class="page-title">BGP Peers</div></dxi-item>
      <dxi-item location="after" widget="dxButton"
                [options]="{ icon: 'refresh', hint: 'Refresh', onClick: refresh }"></dxi-item>
    </dx-toolbar>

    <dx-data-grid [dataSource]="peers" [showBorders]="true" [rowAlternationEnabled]="true"
                  [columnAutoWidth]="true" [filterRow]="{ visible: true }"
                  [searchPanel]="{ visible: true }"
                  [selection]="{ mode: 'single' }"
                  (onSelectionChanged)="onPeerSelected($event)"
                  height="280">
      <dxi-column dataField="hostname" caption="Switch" [fixed]="true" width="200" />
      <dxi-column dataField="local_as" caption="Local AS" width="120" />
      <dxi-column dataField="router_id" caption="Router ID" width="160" />
      <dxi-column dataField="multipath" caption="Multipath" width="100" />
      <dxi-column dataField="last_synced" caption="Last Synced" dataType="datetime" width="180" />
    </dx-data-grid>

    <dx-tab-panel *ngIf="selectedPeer" [items]="tabs" [selectedIndex]="0" [animationEnabled]="false"
                  class="detail-tabs">
      <div *dxTemplate="let tab of 'item'">
        <ng-container [ngSwitch]="tab.key">

          <!-- Neighbors -->
          <div *ngSwitchCase="'neighbors'">
            <dx-data-grid [dataSource]="neighbors" [showBorders]="true" [columnAutoWidth]="true"
                          [filterRow]="{ visible: true }" height="320">
              <dxi-column dataField="neighbor_ip" caption="Neighbor IP" width="180" />
              <dxi-column dataField="remote_as" caption="Remote AS" width="120" />
              <dxi-column dataField="bfd" caption="BFD" width="80" dataType="boolean" />
              <dxi-column dataField="description" caption="Description" />
            </dx-data-grid>
            <div class="empty-state" *ngIf="!neighborsLoading && neighbors.length === 0">
              No neighbors configured for this BGP peer.
            </div>
          </div>

          <!-- Advertised networks -->
          <div *ngSwitchCase="'networks'">
            <dx-data-grid [dataSource]="networks" [showBorders]="true" [columnAutoWidth]="true"
                          [filterRow]="{ visible: true }" height="320">
              <dxi-column dataField="prefix" caption="Prefix" width="220" />
              <dxi-column dataField="description" caption="Description" />
            </dx-data-grid>
            <div class="empty-state" *ngIf="!networksLoading && networks.length === 0">
              No advertised networks configured.
            </div>
          </div>

        </ng-container>
      </div>
    </dx-tab-panel>

    <div class="hint" *ngIf="!selectedPeer">
      Select a row above to see neighbors + advertised networks.
    </div>
  `,
  styles: [`
    .page-toolbar { margin-bottom: 12px; }
    .page-title { font-size: 16px; font-weight: 600; color: #f9fafb; }
    .detail-tabs { margin-top: 16px; }
    .empty-state { text-align: center; color: #6b7280; padding: 24px; font-size: 13px; }
    .hint { color: #6b7280; padding: 16px; font-size: 13px; text-align: center; }
  `]
})
export class BgpPeersComponent implements OnInit {
  peers: BgpPeer[] = [];
  selectedPeer: BgpPeer | null = null;
  neighbors: BgpNeighbor[] = [];
  networks: BgpNetwork[] = [];
  neighborsLoading = false;
  networksLoading = false;

  readonly tabs = [
    { title: 'Neighbors',          key: 'neighbors' },
    { title: 'Advertised Networks', key: 'networks' },
  ];

  constructor(private network: NetworkService) {}

  ngOnInit(): void { this.refresh(); }

  refresh = (): void => {
    this.network.getBgpPeers().subscribe({
      next: p => this.peers = p,
      error: () => notify('Failed to load BGP peers', 'error', 3000)
    });
  };

  onPeerSelected = (e: any): void => {
    this.selectedPeer = e.selectedRowsData?.[0] ?? null;
    if (!this.selectedPeer) {
      this.neighbors = [];
      this.networks = [];
      return;
    }
    const id = this.selectedPeer.id;

    this.neighborsLoading = true;
    this.network.getBgpNeighbors(id).subscribe({
      next: n => { this.neighbors = n; this.neighborsLoading = false; },
      error: () => { this.neighborsLoading = false; notify('Failed to load neighbors', 'error', 3000); }
    });

    this.networksLoading = true;
    this.network.getBgpNetworks(id).subscribe({
      next: n => { this.networks = n; this.networksLoading = false; },
      error: () => { this.networksLoading = false; notify('Failed to load networks', 'error', 3000); }
    });
  };
}
