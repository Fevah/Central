import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, RoomListRow, RackListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Room detail — Summary + Racks tabs. Hierarchy tree → rack
/// drill chain: region → site → building → floor → room → rack.
/// Double-click a rack drills to the rack detail page.
@Component({
  selector: 'app-network-room-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/hierarchy" class="back-link">← Hierarchy</a>
      <h2 *ngIf="room">{{ room.roomCode }}</h2>
      <h2 *ngIf="!room">Loading…</h2>
      <small *ngIf="room" class="subtitle">Room · {{ room.roomType }} · {{ room.status }}</small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             class="tab-bar" />

    <div *ngIf="activeTab === 0 && room" class="meta-grid">
      <div class="meta-row"><label>Room code</label>  <span>{{ room.roomCode }}</span></div>
      <div class="meta-row"><label>Type</label>       <span>{{ room.roomType }}</span></div>
      <div class="meta-row"><label>Max racks</label>  <span>{{ room.maxRacks ?? '—' }}</span></div>
      <div class="meta-row"><label>Floor id</label>   <code>{{ room.floorId }}</code></div>
      <div class="meta-row"><label>Status</label>     <span>{{ room.status }}</span></div>
      <div class="meta-row full"><label>UUID</label>  <code>{{ room.id }}</code></div>
      <div class="meta-row" *ngIf="racksLoaded">
        <label>Racks</label>
        <span>{{ racks.length }}</span>
      </div>
    </div>

    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="racks" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     (onRowDblClick)="onRackDblClick($event)">
        <dxi-column dataField="rackCode"    caption="Rack code"  [fixed]="true" width="140"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="row"         caption="Row"        width="80" />
        <dxi-column dataField="position"    caption="Position"   width="100" dataType="number" />
        <dxi-column dataField="uHeight"     caption="U height"   width="100" dataType="number" />
        <dxi-column dataField="maxDevices"  caption="Max devs"   width="110" dataType="number" />
        <dxi-column dataField="status"      caption="Status"     width="90" />
        <dxi-column dataField="id"          caption="UUID" />
      </dx-data-grid>
      <div *ngIf="racks.length === 0 && !loadingRacks" class="empty-note">
        No racks in this room.
      </div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .tab-bar { margin: 12px 0; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    .empty-note { margin-top: 12px; padding: 10px; color: #64748b; font-size: 12px; background: #0f172a; border-radius: 4px; text-align: center; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkRoomDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Racks' }];
  activeTab = 0;

  roomId = '';
  room: RoomListRow | null = null;
  racks: RackListRow[] = [];
  loadingRacks = false;
  racksLoaded = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.roomId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.roomId) {
      this.status = 'Missing route param — expected /network/room/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listRooms(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.room = rows.find(r => r.id === this.roomId) ?? null;
        this.status = this.room ? '' : 'Room not found.';
        if (this.room) this.loadRacks();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  private loadRacks(): void {
    if (!this.room) return;
    this.loadingRacks = true;
    this.engine.listRacks(environment.defaultTenantId, this.room.id).subscribe({
      next: (rows) => { this.racks = rows; this.loadingRacks = false; this.racksLoaded = true; },
      error: () => { this.loadingRacks = false; this.racksLoaded = true; },
    });
  }

  onRackDblClick(e: { data: RackListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/rack', row.id]);
  }
}
