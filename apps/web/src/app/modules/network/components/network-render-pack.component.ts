import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  DxDataGridModule, DxButtonModule, DxSelectBoxModule,
} from 'devextreme-angular';
import {
  NetworkingEngineService, RenderPackResult, BuildingRow, SiteRow, RegionRow,
} from '../../../core/services/networking-engine.service';
import { environment } from '../../../../environments/environment';

/// Turn-up pack trigger page — fan-out render + persist for every
/// device in a Building / Site / Region. Per-device errors are
/// tolerated (a broken naming template on device 17 doesn't block
/// the other N-1 devices) and surfaced in a separate errors grid.
///
/// Three scopes, three fan-out levels. The form renders the
/// matching picker based on the selected scope. Running a pack
/// writes through the existing scope-aware endpoints — RBAC
/// requires write:{Building/Site/Region} on the target.
@Component({
  selector: 'app-network-render-pack',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule,
            DxDataGridModule, DxButtonModule, DxSelectBoxModule],
  template: `
    <div class="page-header">
      <a routerLink="/network/render-history" class="back-link">← Render history</a>
      <h2>Turn-up pack</h2>
      <small class="subtitle">Render + persist every device in a Building / Site / Region in one call. Per-device errors don't block the rest of the pack.</small>
    </div>

    <div class="form-bar">
      <label>Scope</label>
      <dx-select-box class="md" [items]="scopes" [(value)]="scope"
                     (onValueChanged)="onScopeChanged()" />

      <ng-container *ngIf="scope === 'Building'">
        <label>Building</label>
        <dx-select-box class="lg" [items]="buildings" [(value)]="buildingId"
                       valueExpr="Id" displayExpr="BuildingCode"
                       [searchEnabled]="true" placeholder="Pick a building" />
      </ng-container>
      <ng-container *ngIf="scope === 'Site'">
        <label>Site</label>
        <dx-select-box class="lg" [items]="sites" [(value)]="siteId"
                       valueExpr="Id" displayExpr="SiteCode"
                       [searchEnabled]="true" placeholder="Pick a site" />
      </ng-container>
      <ng-container *ngIf="scope === 'Region'">
        <label>Region</label>
        <dx-select-box class="lg" [items]="regions" [(value)]="regionId"
                       valueExpr="Id" displayExpr="RegionCode"
                       [searchEnabled]="true" placeholder="Pick a region" />
      </ng-container>

      <dx-button text="Run pack" icon="save" type="default"
                 stylingMode="contained"
                 hint="Render + persist every device in the selected scope"
                 (onClick)="runPack()" [disabled]="running || !hasTarget()" />
    </div>

    <div *ngIf="status" class="status-line">{{ status }}</div>

    <!-- Summary cards when a pack has run -->
    <div *ngIf="result" class="summary-cards">
      <div class="card total">
        <div class="card-value">{{ result.totalDevices }}</div>
        <div class="card-label">Devices</div>
      </div>
      <div class="card succeeded">
        <div class="card-value">{{ result.succeeded }}</div>
        <div class="card-label">Succeeded</div>
      </div>
      <div class="card failed">
        <div class="card-value">{{ result.failed }}</div>
        <div class="card-label">Failed</div>
      </div>
    </div>

    <!-- Per-device render rows -->
    <h3 *ngIf="result" class="section-title">Rendered</h3>
    <dx-data-grid *ngIf="result" [dataSource]="result.renders"
                   [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true">
      <dxi-column dataField="flavorCode" caption="Flavor"   width="100" />
      <dxi-column dataField="lineCount"  caption="Lines"    width="80"  dataType="number" />
      <dxi-column dataField="renderedAt" caption="Rendered" width="180" dataType="datetime"
                  format="yyyy-MM-dd HH:mm:ss" />
      <dxi-column dataField="bodySha256" caption="SHA" cellTemplate="shaTemplate" />
      <dxi-column dataField="deviceId"   caption="Device UUID" />

      <div *dxTemplate="let d of 'shaTemplate'">
        <code class="sha">{{ d.value ? d.value.slice(0, 16) + '…' : '—' }}</code>
      </div>
    </dx-data-grid>

    <!-- Per-device errors -->
    <h3 *ngIf="result && result.errors.length > 0" class="section-title error-title">
      Errors ({{ result.errors.length }})
    </h3>
    <dx-data-grid *ngIf="result && result.errors.length > 0"
                   [dataSource]="result.errors"
                   [showBorders]="true" [hoverStateEnabled]="true"
                   [columnAutoWidth]="true">
      <dxi-column dataField="hostname" caption="Hostname" width="220" />
      <dxi-column dataField="error"    caption="Error" cellTemplate="errorTemplate" />
      <dxi-column dataField="deviceId" caption="Device UUID" />

      <div *dxTemplate="let d of 'errorTemplate'">
        <span class="err-text">{{ d.value }}</span>
      </div>
    </dx-data-grid>
  `,
  styles: [`
    .page-header { margin-bottom: 12px; }
    .page-header h2 { margin: 0 0 4px 0; }
    .back-link { color: #3b82f6; text-decoration: none; font-size: 12px; }
    .back-link:hover { text-decoration: underline; }
    .subtitle { color: #888; }
    .form-bar { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 10px; }
    .form-bar label { color: #888; font-size: 12px; }
    .form-bar .md { width: 140px; }
    .form-bar .lg { width: 280px; }
    .status-line { margin: 6px 0 10px; color: #666; font-size: 12px; }
    .summary-cards { display: flex; gap: 16px; margin: 16px 0; }
    .card { flex: 1; padding: 16px; border-radius: 8px; text-align: center; }
    .card.total     { background: rgba(59,130,246,0.1); border: 1px solid rgba(59,130,246,0.3); }
    .card.succeeded { background: rgba(34,197,94,0.1); border: 1px solid rgba(34,197,94,0.3); }
    .card.failed    { background: rgba(239,68,68,0.1); border: 1px solid rgba(239,68,68,0.3); }
    .card-value { font-size: 28px; font-weight: bold; }
    .card.total .card-value     { color: #60a5fa; }
    .card.succeeded .card-value { color: #22c55e; }
    .card.failed .card-value    { color: #ef4444; }
    .card-label { font-size: 11px; color: #9ca3af; text-transform: uppercase; letter-spacing: 0.5px; }
    .section-title { color: #9ca3af; font-size: 13px; text-transform: uppercase; letter-spacing: 0.5px; margin: 20px 0 8px; font-weight: 600; }
    .section-title.error-title { color: #ef4444; }
    .sha { color: #94a3b8; font-family: ui-monospace, monospace; font-size: 11px; }
    .err-text { color: #ef4444; font-family: ui-monospace, monospace; font-size: 12px; }
  `]
})
export class NetworkRenderPackComponent implements OnInit {
  scopes = ['Building', 'Site', 'Region'];
  scope: 'Building' | 'Site' | 'Region' = 'Building';

  buildings: BuildingRow[] = [];
  sites: SiteRow[] = [];
  regions: RegionRow[] = [];
  buildingId: string | null = null;
  siteId: string | null = null;
  regionId: string | null = null;

  running = false;
  status = '';
  result: RenderPackResult | null = null;

  constructor(private engine: NetworkingEngineService) {}

  ngOnInit(): void {
    // Prime all three scope pickers up-front so switching scopes
    // is instant. Hierarchy endpoints are tenant-sized + tiny.
    this.engine.listBuildings().subscribe({
      next: (rows) => { this.buildings = rows; }, error: () => {},
    });
    this.engine.listSites().subscribe({
      next: (rows) => { this.sites = rows; }, error: () => {},
    });
    this.engine.listRegions().subscribe({
      next: (rows) => { this.regions = rows; }, error: () => {},
    });
  }

  hasTarget(): boolean {
    return (this.scope === 'Building' && !!this.buildingId) ||
           (this.scope === 'Site'     && !!this.siteId) ||
           (this.scope === 'Region'   && !!this.regionId);
  }

  onScopeChanged(): void {
    this.result = null;
    this.status = '';
  }

  runPack(): void {
    this.running = true;
    this.result = null;
    this.status = `Rendering ${this.scope.toLowerCase()} pack…`;
    const obs =
      this.scope === 'Building' && this.buildingId
        ? this.engine.renderBuildingConfigs(this.buildingId, environment.defaultTenantId)
    : this.scope === 'Site'     && this.siteId
        ? this.engine.renderSiteConfigs(this.siteId, environment.defaultTenantId)
    : this.scope === 'Region'   && this.regionId
        ? this.engine.renderRegionConfigs(this.regionId, environment.defaultTenantId)
    : null;

    if (!obs) { this.running = false; return; }

    obs.subscribe({
      next: (r) => {
        this.running = false;
        this.result = r;
        this.status = `Pack complete — ${r.succeeded}/${r.totalDevices} succeeded.`;
      },
      error: (err) => {
        this.running = false;
        this.status = err?.status === 403
          ? `Forbidden — lacks write:${this.scope} on this scope.`
          : `Pack failed: ${err?.error?.detail ?? err?.message ?? err}`;
      },
    });
  }
}
