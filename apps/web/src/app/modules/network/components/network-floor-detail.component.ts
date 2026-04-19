import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, FloorRow, RoomListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Floor detail — region → site → building → floor drill chain.
/// Two tabs: Summary (row fields) + Rooms (every net.room under
/// this floor). Rack detail is one more drill step from a room.
///
/// Routed at /network/floor/:id.
@Component({
  selector: 'app-network-floor-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/hierarchy" class="back-link">← Hierarchy</a>
      <h2 *ngIf="floor">{{ floor.FloorCode }}<span *ngIf="floor.DisplayName"> · {{ floor.DisplayName }}</span></h2>
      <h2 *ngIf="!floor">Loading…</h2>
      <small *ngIf="floor" class="subtitle">Floor · {{ floor.Status }}</small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <dx-tabs [dataSource]="tabs" [(selectedIndex)]="activeTab"
             class="tab-bar" />

    <!-- Summary tab -->
    <div *ngIf="activeTab === 0 && floor" class="meta-grid">
      <div class="meta-row"><label>Floor code</label>   <span>{{ floor.FloorCode }}</span></div>
      <div class="meta-row"><label>Display name</label> <span>{{ floor.DisplayName ?? '—' }}</span></div>
      <div class="meta-row"><label>Building id</label>  <code>{{ floor.BuildingId }}</code></div>
      <div class="meta-row"><label>Status</label>       <span>{{ floor.Status }}</span></div>
      <div class="meta-row full"><label>UUID</label>    <code>{{ floor.Id }}</code></div>
      <div class="meta-row" *ngIf="roomsLoaded">
        <label>Rooms</label>
        <span>{{ rooms.length }}</span>
      </div>
    </div>

    <!-- Rooms tab -->
    <div *ngIf="activeTab === 1">
      <dx-data-grid [dataSource]="rooms" [showBorders]="true" [hoverStateEnabled]="true"
                     [columnAutoWidth]="true"
                     [searchPanel]="{ visible: true }"
                     [filterRow]="{ visible: true }"
                     (onRowDblClick)="onRoomDblClick($event)">
        <dxi-column dataField="roomCode"  caption="Room code" [fixed]="true" width="140"
                    sortOrder="asc" [sortIndex]="0" />
        <dxi-column dataField="roomType"  caption="Type"      width="120" />
        <dxi-column dataField="maxRacks"  caption="Max racks" width="110" dataType="number" />
        <dxi-column dataField="status"    caption="Status"    width="90" />
        <dxi-column dataField="id"        caption="UUID" />
      </dx-data-grid>
      <div *ngIf="rooms.length === 0 && !loadingRooms" class="empty-note">
        No rooms recorded for this floor.
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
export class NetworkFloorDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }, { text: 'Rooms' }];
  activeTab = 0;

  floorId = '';
  floor: FloorRow | null = null;
  rooms: RoomListRow[] = [];
  loadingRooms = false;
  roomsLoaded = false;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.floorId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.floorId) {
      this.status = 'Missing route param — expected /network/floor/:id.';
      return;
    }
    this.status = 'Loading…';

    this.engine.listFloors().subscribe({
      next: (rows) => {
        this.floor = rows.find(r => r.Id === this.floorId) ?? null;
        this.status = this.floor ? '' : 'Floor not found.';
        if (this.floor) this.loadRooms();
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }

  private loadRooms(): void {
    if (!this.floor) return;
    this.loadingRooms = true;
    this.engine.listRooms(environment.defaultTenantId, this.floor.Id).subscribe({
      next: (rows) => { this.rooms = rows; this.loadingRooms = false; this.roomsLoaded = true; },
      error: () => { this.loadingRooms = false; this.roomsLoaded = true; },
    });
  }

  onRoomDblClick(e: { data: RoomListRow }): void {
    const row = e?.data;
    if (!row?.id) return;
    this.router.navigate(['/network/room', row.id]);
  }
}
