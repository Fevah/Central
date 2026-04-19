import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  DxButtonModule, DxTabsModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, ModuleListRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Hardware module detail page. Single tab (Summary) — audit
/// drill goes through the device's audit since net.module rows
/// don't get their own audit_entry rows today.
///
/// Routed at /network/module/:id.
@Component({
  selector: 'app-network-module-detail',
  standalone: true,
  imports: [CommonModule, RouterModule,
            DxButtonModule, DxTabsModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/modules" class="back-link">← Modules</a>
      <h2 *ngIf="module">{{ module.slot }} · {{ module.moduleType }}</h2>
      <h2 *ngIf="!module">Loading…</h2>
      <small *ngIf="module" class="subtitle">
        on <a [routerLink]="['/network/net-device', module.deviceId]" class="device-link">{{ module.deviceHostname ?? module.deviceId }}</a>
      </small>
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <div *ngIf="module" class="meta-grid">
      <div class="meta-row"><label>Slot</label>        <span>{{ module.slot }}</span></div>
      <div class="meta-row"><label>Type</label>        <span>{{ module.moduleType }}</span></div>
      <div class="meta-row"><label>Device</label>
        <a [routerLink]="['/network/net-device', module.deviceId]" class="device-link">
          {{ module.deviceHostname ?? module.deviceId }}
        </a>
      </div>
      <div class="meta-row"><label>Model</label>       <span>{{ module.model ?? '—' }}</span></div>
      <div class="meta-row"><label>Part number</label> <span>{{ module.partNumber ?? '—' }}</span></div>
      <div class="meta-row"><label>Serial</label>      <span>{{ module.serialNumber ?? '—' }}</span></div>
      <div class="meta-row"><label>Status</label>      <span>{{ module.status }}</span></div>
      <div class="meta-row"><label>Version</label>     <span>{{ module.version }}</span></div>
      <div class="meta-row full"><label>UUID</label>   <code>{{ module.id }}</code></div>
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
    .device-link { color: #60a5fa; text-decoration: none; }
    .device-link:hover { text-decoration: underline; }
    @media (max-width: 1100px) { .meta-grid { grid-template-columns: 1fr 1fr; } }
  `]
})
export class NetworkModuleDetailComponent implements OnInit {
  moduleId = '';
  module: ModuleListRow | null = null;
  status = '';

  constructor(
    private route: ActivatedRoute,
    private _router: Router,
    private engine: NetworkingEngineService,
  ) {}

  ngOnInit(): void {
    this.moduleId = this.route.snapshot.paramMap.get('id') ?? '';
    if (!this.moduleId) {
      this.status = 'Missing route param — expected /network/module/:id.';
      return;
    }
    this.status = 'Loading…';
    this.engine.listModules(environment.defaultTenantId).subscribe({
      next: (rows) => {
        this.module = rows.find(r => r.id === this.moduleId) ?? null;
        this.status = this.module ? '' : 'Module not found.';
      },
      error: (err) => { this.status = `Load failed: ${err?.message ?? err}`; },
    });
  }
}
