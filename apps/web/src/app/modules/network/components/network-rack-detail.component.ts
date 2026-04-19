import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, RackListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Rack detail — bottom of the hierarchy drill chain. Single
/// Summary tab; the "devices in this rack" drill lands when
/// net.device gains a rack_id column (today devices are tied to
/// buildings, so per-rack filtering isn't direct — future slice
/// once the rack linkage ships in the data model).
@Component({
  selector: 'app-network-rack-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/hierarchy" class="back-link">← Hierarchy</a>
      <h2 *ngIf="rack">{{ rack.rackCode }}</h2>
      <h2 *ngIf="!rack">Loading…</h2>
      <small *ngIf="rack" class="subtitle">Rack · {{ rack.uHeight }}U · {{ rack.status }}</small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <div *ngIf="rack" class="meta-grid">
      <div class="meta-row"><label>Rack code</label>  <span>{{ rack.rackCode }}</span></div>
      <div class="meta-row"><label>Row</label>        <span>{{ rack.row ?? '—' }}</span></div>
      <div class="meta-row"><label>Position</label>   <span>{{ rack.position ?? '—' }}</span></div>
      <div class="meta-row"><label>U height</label>   <span>{{ rack.uHeight }}</span></div>
      <div class="meta-row"><label>Max devices</label><span>{{ rack.maxDevices ?? '—' }}</span></div>
      <div class="meta-row"><label>Status</label>     <span>{{ rack.status }}</span></div>
      <div class="meta-row full"><label>Room id</label><code>{{ rack.roomId }}</code></div>
      <div class="meta-row full"><label>UUID</label>  <code>{{ rack.id }}</code></div>
    </div>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .status-line { color: #666; font-size: 12px; margin: 6px 0 10px; }
    .meta-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px 16px; padding: 12px; background: #1e293b; border-radius: 6px; }
    .meta-row { display: flex; flex-direction: column; gap: 2px; }
    .meta-row.full { grid-column: 1 / -1; }
    .meta-row label { color: #64748b; font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; }
    .meta-row code { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 12px; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkRackDetailComponent implements OnInit {
  rackId = '';
  rack: RackListRow | null = null;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private _router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.rackId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.rackId) {
      this.status = 'Missing route param — expected /network/rack/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listRacks(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.rack = rows.find(r => r.id === this.rackId) ?? null;
        this.status = this.rack ? '' : 'Rack not found.';
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }
}
