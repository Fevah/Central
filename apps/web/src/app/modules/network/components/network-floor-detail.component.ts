import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, FloorRow,
} from '../../../core/services/networking-engine.service';

/// Floor detail — bottom of the hierarchy detail-page chain
/// (region → site → building → floor). One tab (Summary) for this
/// slice; Room + Rack tabs land when the engine gains thin lists
/// for those levels.
///
/// Routed at /network/floor/:id. Hierarchy tree's Floor-node
/// double-click currently falls through to audit; a follow-up
/// wires it here.
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
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkFloorDetailComponent implements OnInit {
  tabs = [{ text: 'Summary' }];
  activeTab = 0;

  floorId = '';
  floor: FloorRow | null = null;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private _router: Router,
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
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }
}
